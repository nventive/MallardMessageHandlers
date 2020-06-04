using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MallardMessageHandlers;
using Xunit;

namespace MallardMessageHandlers.Tests
{
	public class AuthenticationTokenHandlerTests
	{
		private const string DefaultRequestUri = "http://wwww.test.com";

		[Fact]
		public async Task It_AddsAccessToken_If_Token()
		{
			var authenticationToken = new TestToken("AccessToken1");
			var authorizationHeader = default(AuthenticationHeaderValue);

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(authenticationToken);

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) =>
				{
					authorizationHeader = r.Headers.Authorization;

					return Task.FromResult(new HttpResponseMessage());
				}));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			authorizationHeader.Parameter.Should().Be(authenticationToken.AccessToken);
		}

		[Fact]
		public async Task It_RemovesAuthorizationHeader_If_NoToken()
		{
			var authorizationHeader = default(AuthenticationHeaderValue);

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(default(TestToken));

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) =>
				{
					authorizationHeader = r.Headers.Authorization;

					return Task.FromResult(new HttpResponseMessage());
				}));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			authorizationHeader.Should().BeNull();
		}

		[Fact]
		public async Task It_Doesnt_GetToken_If_NoAuthorization()
		{
			var isTokenRequested = false;
			var authenticationToken = new TestToken("AccessToken1");

			Task<TestToken> GetToken(HttpRequestMessage request)
			{
				isTokenRequested = true;

				return Task.FromResult(authenticationToken);
			}

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) => Task.FromResult(new HttpResponseMessage())));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			await httpClient.GetAsync(DefaultRequestUri);

			isTokenRequested.Should().BeFalse();
		}

		[Fact]
		public async Task It_Doesnt_RefreshToken_If_Authorized()
		{
			var refreshedToken = false;
			var authenticationToken = new TestToken("AccessToken1", "RefreshToken1");
			var refreshedAuthenticationToken = new TestToken("AccessToken2", "RefreshToken2");

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(authenticationToken);

			Task<TestToken> RefreshToken(HttpRequestMessage request, TestToken token)
			{
				refreshedToken = true;

				return Task.FromResult(refreshedAuthenticationToken);
			}

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken, RefreshToken))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) => Task.FromResult(new HttpResponseMessage())));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			refreshedToken.Should().BeFalse();
		}

		[Fact]
		public async Task It_Retries_With_RefreshedToken()
		{
			var authenticationToken = new TestToken("AccessToken1", "RefreshToken1");
			var refreshedAuthenticationToken = new TestToken("AccessToken2", "RefreshToken2");
			var authorizationHeaders = new List<AuthenticationHeaderValue>();

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(authenticationToken);

			Task<TestToken> RefreshToken(HttpRequestMessage request, TestToken token)
				=> Task.FromResult(refreshedAuthenticationToken);

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken, RefreshToken))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) =>
				{
					authorizationHeaders.Add(r.Headers.Authorization);

					var isUnauthorized = r.Headers.Authorization.Parameter == authenticationToken.AccessToken;

					return Task.FromResult(new HttpResponseMessage(isUnauthorized ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
				}));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			authorizationHeaders.Count.Should().Be(2);
			authorizationHeaders.First().Parameter.Should().Be(authenticationToken.AccessToken);
			authorizationHeaders.ElementAt(1).Parameter.Should().Be(refreshedAuthenticationToken.AccessToken);
		}

		[Fact]
		public async Task It_NotifiesSessionExpired_If_Unauthorized_And_CantRefresh()
		{
			var sessionExpired = false;
			var authenticationToken = new TestToken("AccessToken1");

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(authenticationToken);

			Task SessionExpired(HttpRequestMessage request, TestToken token)
			{
				sessionExpired = true;

				return Task.CompletedTask;
			}

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken, sessionExpired: SessionExpired))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			sessionExpired.Should().BeTrue();
		}

		[Fact]
		public async Task It_NotifiesSessionExpired_If_Refreshed_And_NoToken()
		{
			var sessionExpired = false;
			var refreshedToken = false;
			var authenticationToken = new TestToken("AccessToken1", "RefreshToken1");

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(authenticationToken);

			Task<TestToken> RefreshToken(HttpRequestMessage request, TestToken token)
			{
				refreshedToken = true;

				return Task.FromResult(default(TestToken));
			}

			Task SessionExpired(HttpRequestMessage request, TestToken token)
			{
				sessionExpired = true;

				return Task.CompletedTask;
			}

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken, RefreshToken, SessionExpired))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			refreshedToken.Should().BeTrue();
			sessionExpired.Should().BeTrue();
		}

		[Fact]
		public async Task It_NotifiesSessionExpired_If_Refreshed_And_Unauthorized()
		{
			var sessionExpired = false;
			var authenticationToken = new TestToken("AccessToken1", "RefreshToken1");
			var refreshedAuthenticationToken = new TestToken("AccessToken2", "RefreshToken2");
			var authorizationHeaders = new List<AuthenticationHeaderValue>();

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(authenticationToken);

			Task<TestToken> RefreshToken(HttpRequestMessage request, TestToken token)
				=> Task.FromResult(refreshedAuthenticationToken);

			Task SessionExpired(HttpRequestMessage request, TestToken token)
			{
				sessionExpired = true;

				return Task.CompletedTask;
			}

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken, RefreshToken, SessionExpired))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) =>
				{
					authorizationHeaders.Add(r.Headers.Authorization);

					var isUnauthorized = r.Headers.Authorization.Parameter == authenticationToken.AccessToken;

					return Task.FromResult(new HttpResponseMessage(isUnauthorized ? HttpStatusCode.Unauthorized : HttpStatusCode.Unauthorized));
				}));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			authorizationHeaders.Count.Should().Be(2);
			sessionExpired.Should().BeTrue();
		}

		[Fact]
		public async Task It_NotifiesSessionExpired_If_Refresh_Throws()
		{
			var sessionExpired = false;
			var refreshedToken = false;
			var authenticationToken = new TestToken("AccessToken1", "RefreshToken1");

			Task<TestToken> GetToken(HttpRequestMessage request)
				=> Task.FromResult(authenticationToken);

			Task<TestToken> RefreshToken(HttpRequestMessage request, TestToken token)
			{
				refreshedToken = true;

				throw new TestException();
			}

			Task SessionExpired(HttpRequestMessage request, TestToken token)
			{
				sessionExpired = true;

				return Task.CompletedTask;
			}

			void BuildServices(IServiceCollection s) => s
				.AddSingleton<IAuthenticationTokenProvider<TestToken>>(new AuthenticationTokenProvider(GetToken, RefreshToken, SessionExpired))
				.AddTransient<AuthenticationTokenHandler<TestToken>>()
				.AddTransient(_ => new TestHandler((r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));

			void BuildHttpClient(IHttpClientBuilder h) => h
				.AddHttpMessageHandler<AuthenticationTokenHandler<TestToken>>()
				.AddHttpMessageHandler<TestHandler>();

			var httpClient = HttpClientTestsHelper.GetTestHttpClient(BuildServices, BuildHttpClient);

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

			await httpClient.GetAsync(DefaultRequestUri);

			refreshedToken.Should().BeTrue();
			sessionExpired.Should().BeTrue();
		}

		private class AuthenticationTokenProvider : IAuthenticationTokenProvider<TestToken>
		{
			private readonly Func<HttpRequestMessage, Task<TestToken>> _getToken;
			private readonly Func<HttpRequestMessage, TestToken, Task<TestToken>> _refreshToken;
			private readonly Func<HttpRequestMessage, TestToken, Task> _sessionExpired;

			public AuthenticationTokenProvider(
				Func<HttpRequestMessage, Task<TestToken>> getToken = null,
				Func<HttpRequestMessage, TestToken, Task<TestToken>> refreshToken = null,
				Func<HttpRequestMessage, TestToken, Task> sessionExpired = null
			)
			{
				_getToken = getToken;
				_refreshToken = refreshToken;
				_sessionExpired = sessionExpired;
			}

			public Task<TestToken> GetToken(CancellationToken ct, HttpRequestMessage request)
				=> _getToken?.Invoke(request);

			public Task<TestToken> RefreshToken(CancellationToken ct, HttpRequestMessage request, TestToken unauthorizedToken)
				=> _refreshToken?.Invoke(request, unauthorizedToken);

			public Task NotifySessionExpired(CancellationToken ct, HttpRequestMessage request, TestToken unauthorizedToken)
				=> _sessionExpired?.Invoke(request, unauthorizedToken);
		}
	}
}
