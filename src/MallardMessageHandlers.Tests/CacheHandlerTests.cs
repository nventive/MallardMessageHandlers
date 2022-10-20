using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MallardMessageHandlers.SimpleCaching;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace MallardMessageHandlers.Tests
{
	public class CacheHandlerTests
	{
		private const string DefaultRequestUri = "http://wwww.test.com";
		private const string Cache5Minutes = SimpleCacheHandler.CacheTimeToLiveHeaderName + ": 300";
		private const string Cache10Minutes = SimpleCacheHandler.CacheTimeToLiveHeaderName + ": 600";
		private const string ForceRefresh = SimpleCacheHandler.CacheForceRefreshHeaderName + ": true";
		private const string DisableCache = SimpleCacheHandler.CacheDisableHeaderName + ": true";

		[Fact]
		public async Task It_doesnt_invoke_cache_service_when_ttl_header_isnt_present()
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>();
			var httpClient = GetClient(cacheServiceMock);

			// Act
			await httpClient.GetAsync(DefaultRequestUri);

			// Assert
			var x = default(byte[]);
			cacheServiceMock.Verify(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out x), Times.Never);
			cacheServiceMock.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task It_doesnt_invoke_cache_service_when_cache_disable_header_is_present()
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>();
			var httpClient = GetClient(cacheServiceMock, Cache5Minutes, DisableCache);

			// Act
			await httpClient.GetAsync(DefaultRequestUri);

			// Assert
			var x = default(byte[]);
			cacheServiceMock.Verify(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out x), Times.Never);
			cacheServiceMock.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task Cache_service_is_invoked_when_ttl_header_is_present()
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>();
			var httpClient = GetClient(cacheServiceMock, Cache5Minutes);

			// Act
			await httpClient.GetAsync(DefaultRequestUri);
			
			// Assert
			var x = default(byte[]);
			cacheServiceMock.Verify(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out x), Times.Once);
			cacheServiceMock.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<byte[]>(), It.Is<TimeSpan>(ts => ts.TotalMinutes == 5), It.IsAny<CancellationToken>()), Times.Once);
		}

		[Fact]
		public async Task Cache_service_is_invoked_most_up_to_date_TTL_value()
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>();
			// 5 Minutes is the default, 10 minutes is the override. 10 minutes should be used.
			var httpClient = GetClientWithExtraHeader(cacheServiceMock, insertedHeader: Cache10Minutes, Cache5Minutes);

			// Act
			await httpClient.GetAsync(DefaultRequestUri);

			// Assert
			cacheServiceMock.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<byte[]>(), It.Is<TimeSpan>(ts => ts.TotalMinutes == 10), It.IsAny<CancellationToken>()), Times.Once);
		}

		[Fact]
		public async Task Inner_handler_isnt_called_when_cache_is_active()
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>();
			var payload = new byte[] { 1, 2, 3 };
			cacheServiceMock.Setup(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out payload)).ReturnsAsync(true);
			var innerHandlerWasInvoked = new TaskCompletionSource<bool>();			

			var httpClient = GetClient(cacheServiceMock, innerHandlerWasInvoked, Cache5Minutes);

			// Act
			var result = await httpClient.GetAsync(DefaultRequestUri);

			// Assert
			innerHandlerWasInvoked.Task.Status.Should().NotBe(TaskStatus.RanToCompletion);
			(await result.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(payload);
		}

		[Fact]
		public async Task InnerHandler_is_called_and_Cache_is_updated_when_ForceRefresh_and_CacheTTL_headers_are_present()
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>();
			var payload = new byte[] { 1, 2, 3 };
			cacheServiceMock.Setup(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out payload)).ReturnsAsync(true);
			var innerHandlerWasInvoked = new TaskCompletionSource<bool>();

			var httpClient = GetClient(cacheServiceMock, innerHandlerWasInvoked, Cache5Minutes, ForceRefresh);

			// Act
			var result = await httpClient.GetAsync(DefaultRequestUri);

			// Assert
			innerHandlerWasInvoked.Task.Status.Should().Be(TaskStatus.RanToCompletion);
			(await result.Content.ReadAsStringAsync()).Should().Be("Hello");
			cacheServiceMock.Verify(s => s.Add(
				It.IsAny<string>(),
				It.Is<byte[]>(bytes => bytes.SequenceEqual(UTF8Encoding.UTF8.GetBytes("Hello"))),
				It.IsAny<TimeSpan>(),
				It.IsAny<CancellationToken>()),
			Times.Once);
		}

		private static HttpClient GetClient(Mock<ISimpleCacheService> cacheServiceMock, params string[] headers)
			=> GetClient(cacheServiceMock, new TaskCompletionSource<bool>(), headers);

		private static HttpClient GetClient(Mock<ISimpleCacheService> cacheServiceMock, TaskCompletionSource<bool> innerHandlerWasInvoked, params string[] headers)
		{
			void BuildServices(IServiceCollection s) => s
				.AddSingleton(cacheServiceMock.Object)
				.AddSingleton(SimpleCacheKeyProvider.FromUriOnly)
				.AddTransient(_ => new TestHandler((r, ct) =>
				{
					innerHandlerWasInvoked.TrySetResult(true);
					return Task.FromResult(new HttpResponseMessage()
					{
						Content = new StringContent("Hello")
					});
				}))
				.AddTransient<SimpleCacheHandler>();

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<SimpleCacheHandler>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);
			foreach (var header in headers)
			{
				var parts = header.Split(':');
				var name = parts[0];
				var value = parts[1];
				httpClient.DefaultRequestHeaders.Add(name, value);
			}
			return httpClient;
		}

		private static HttpClient GetClientWithExtraHeader(Mock<ISimpleCacheService> cacheServiceMock, string insertedHeader, params string[] headers)
		{
			void BuildServices(IServiceCollection s) => s
				.AddSingleton(cacheServiceMock.Object)
				.AddSingleton(SimpleCacheKeyProvider.FromUriOnly)
				.AddTransient(_ => new TestHandler((r, ct) =>
				{
					return Task.FromResult(new HttpResponseMessage()
					{
						Content = new StringContent("Hello")
					});
				}))
				.AddTransient<SimpleCacheHandler>();

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler(() => new HeaderInserterHandler(insertedHeader))
				.AddHttpMessageHandler<SimpleCacheHandler>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);
			foreach (var header in headers)
			{
				var parts = header.Split(':');
				var name = parts[0];
				var value = parts[1];
				httpClient.DefaultRequestHeaders.Add(name, value);
			}
			return httpClient;
		}

		private class HeaderInserterHandler : DelegatingHandler
		{
			private readonly string _header;

			public HeaderInserterHandler(string header)
			{
				_header = header;
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				var parts = _header.Split(':');
				var name = parts[0];
				var value = parts[1];
				request.Headers.Add(name, value);
				return base.SendAsync(request, cancellationToken);
			}		
		}
	}
}
