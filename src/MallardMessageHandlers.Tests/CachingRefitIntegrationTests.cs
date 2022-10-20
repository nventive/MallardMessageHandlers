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
using Refit;
using Xunit;

namespace MallardMessageHandlers.Tests
{
	public interface ISampleEndpoint
	{
		// Default cache is 10 minutes on all methods.

		[Get("/sample")]
		Task<string> GetSampleDefault(CancellationToken ct, [ForceRefresh] bool forceRefresh = false);

		[Get("/sample")]
		[TimeToLive(totalMinutes: 5)] // You can customize the TTL on a per-call basis.
		Task<string> GetSampleCustomTTL(CancellationToken ct, [ForceRefresh] bool forceRefresh = false);

		[Get("/sample")]
		[NoCache] // When you have a default TTL, you can bypass it on a per-call basis.
		Task<string> GetSampleNoCache(CancellationToken ct);
	}

	public class CachingRefitIntegrationTests
	{
		public static IEnumerable<object[]> GetDataFor_GetSampleCache_x1()
		{
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleDefault(CancellationToken.None)), 10 };
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleCustomTTL(CancellationToken.None)), 5 };
		}

		[Theory]
		[MemberData(nameof(GetDataFor_GetSampleCache_x1))]
		public async Task GetSampleCached_x1(Func<ISampleEndpoint, Task<string>> action, int ttl)
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>(() => new MemorySimpleCacheService());
			var endpoint = Setup(cacheServiceMock.Object);

			// Act
			var result = await action(endpoint);

			// Assert
			res­­ult.Should().Be("1");
			var x = default(byte[]);
			cacheServiceMock.Verify(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out x), Times.Once);
			cacheServiceMock.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<byte[]>(), It.Is<TimeSpan>(ts => ts.TotalMinutes == ttl), It.IsAny<CancellationToken>()), Times.Once);
		}

		public static IEnumerable<object[]> GetDataFor_GetSampleCache_x2()
		{
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleDefault(CancellationToken.None)) };
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleCustomTTL(CancellationToken.None))};
		}

		[Theory]
		[MemberData(nameof(GetDataFor_GetSampleCache_x2))]
		public async Task GetSampleCached_x2(Func<ISampleEndpoint, Task<string>> action)
		{
			// Arrange
			var endpoint = Setup();

			// Act
			var result1 = await action(endpoint);
			var result2 = await action(endpoint);

			// Assert
			res­­ult1.Should().Be("1");
			res­­ult2.Should().Be("1");
		}

		public static IEnumerable<object[]> GetDataFor_GetSampleCached_with_force_refresh_x1()
		{
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleDefault(CancellationToken.None, forceRefresh: true)), 10 };
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleCustomTTL(CancellationToken.None, forceRefresh: true)), 5 };
		}

		[Theory]
		[MemberData(nameof(GetDataFor_GetSampleCached_with_force_refresh_x1))]
		public async Task GetSampleCached_with_force_refresh_x1(Func<ISampleEndpoint, Task<string>> action, int ttl)
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>(() => new MemorySimpleCacheService());
			var endpoint = Setup(cacheServiceMock.Object);

			// Act
			var result1 = await action(endpoint);

			// Assert
			var x = default(byte[]);
			cacheServiceMock.Verify(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out x), Times.Never);
			cacheServiceMock.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<byte[]>(), It.Is<TimeSpan>(ts => ts.TotalMinutes == ttl), It.IsAny<CancellationToken>()), Times.Once);
		}

		public static IEnumerable<object[]> GetDataFor_GetSampleCached_with_force_refresh_x2()
		{
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleDefault(CancellationToken.None, forceRefresh: true)) };
			yield return new object[] { (Func<ISampleEndpoint, Task<string>>)(endpoint => endpoint.GetSampleCustomTTL(CancellationToken.None, forceRefresh: true)) };
		}

		[Theory]
		[MemberData(nameof(GetDataFor_GetSampleCached_with_force_refresh_x2))]
		public async Task GetSampleCached_with_force_refresh_x2(Func<ISampleEndpoint, Task<string>> action)
		{
			// Arrange
			var endpoint = Setup();

			// Act
			var result1 = await action(endpoint);
			var result2 = await action(endpoint);

			// Assert
			res­­ult1.Should().Be("1");
			res­­ult2.Should().Be("2");
		}

		[Fact]
		public async Task GetSampleNoCache_x1()
		{
			// Arrange
			var cacheServiceMock = new Mock<ISimpleCacheService>(() => new MemorySimpleCacheService());
			var endpoint = Setup(cacheServiceMock.Object);

			// Act
			var result = await endpoint.GetSampleNoCache(CancellationToken.None);

			// Assert
			res­­ult.Should().Be("1");
			var x = default(byte[]);
			cacheServiceMock.Verify(s => s.TryGet(It.IsAny<string>(), It.IsAny<CancellationToken>(), out x), Times.Never);
			cacheServiceMock.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task GetSampleNoCache_x2()
		{
			// Arrange
			var endpoint = Setup();

			// Act
			var result1 = await endpoint.GetSampleNoCache(CancellationToken.None);
			var result2 = await endpoint.GetSampleNoCache(CancellationToken.None);

			// Assert
			res­­ult1.Should().Be("1");
			res­­ult2.Should().Be("2");
		}

		private ISampleEndpoint Setup(ISimpleCacheService cacheService = null)
		{
			int callCount = 0;
			var serviceCollection = new ServiceCollection()
				.AddSingleton<ISimpleCacheService>(cacheService ?? new MemorySimpleCacheService())
				.AddSingleton(SimpleCacheKeyProvider.FromUriAndAuthorizationHash)
				.AddTransient(_ => new TestHandler((request, ct) =>
				{
					var response = new HttpResponseMessage()
					{
						Content = new StringContent($"{++callCount}")
					};
					return Task.FromResult(response);
				}))
				.AddTransient<SimpleCacheHandler>();

			serviceCollection
				.AddRefitClient<ISampleEndpoint>()
				.ConfigureHttpClient((client) =>
				{
					client.BaseAddress = new Uri("http://localhost");
					client.DefaultRequestHeaders.Add("Authorization", "Bearer 123");
					client.DefaultRequestHeaders.Add(SimpleCacheHandler.CacheTimeToLiveHeaderName, "600");
				})
				.ConfigurePrimaryHttpMessageHandler<TestHandler>()
				.AddHttpMessageHandler<SimpleCacheHandler>();

			var serviceProvider = serviceCollection.BuildServiceProvider();
			return serviceProvider.GetRequiredService<ISampleEndpoint>();
		}
	}
}
