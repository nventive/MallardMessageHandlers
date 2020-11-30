using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MallardMessageHandlers
{
	/// <summary>
	/// This <see cref="HttpMessageHandler"/> handles the access token logic.
	/// If the request should include an authentication token <see cref="ShouldIncludeToken(HttpRequestMessage)"/>,
	/// the handler gets the token from the <see cref="IAuthenticationTokenProvider{TAuthenticationToken}"/>.
	/// By default, a token is added if the request contains the Authorization header.
	/// If a token is returned, the token is added to the header. Otherwise, the header is removed.
	/// If the response is considered unauthorized <see cref="IsUnauthorized(HttpRequestMessage, HttpResponseMessage)"/>
	/// and the token can be refreshed, the token is refreshed and the request is retried with the new token.
	/// If the request is still considered unauthorized or the token cannot be refreshed, the session is
	/// considered expired and the <see cref="IAuthenticationTokenProvider{TAuthenticationToken}"/> is notified.
	/// </summary>
	/// <typeparam name="TAuthenticationToken">Type of authentication token</typeparam>
	[Preserve(AllMembers = true)]
	public class AuthenticationTokenHandler<TAuthenticationToken> : DelegatingHandler
		where TAuthenticationToken : IAuthenticationToken
	{
		private readonly IAuthenticationTokenProvider<TAuthenticationToken> _tokenProvider;
		private readonly ILogger _logger;
		private readonly AuthenticationTokenHandlerContext _context;

		/// <summary>
		/// Initializes a new instance of the <see cref="AuthenticationTokenHandler{TAuthenticationToken}"/> class.
		/// </summary>
		/// <param name="tokenProvider"><see cref="IAuthenticationTokenProvider{TAuthenticationToken}"/></param>
		/// <param name="logger"><see cref="ILogger"/></param>
		/// <param name="context"><see cref="AuthenticationTokenHandlerContext"/></param>
		public AuthenticationTokenHandler(
			IAuthenticationTokenProvider<TAuthenticationToken> tokenProvider,
			ILogger<AuthenticationTokenHandler<TAuthenticationToken>> logger,
			AuthenticationTokenHandlerContext context = null
		)
		{
			_tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
			_logger = logger ?? NullLogger<AuthenticationTokenHandler<TAuthenticationToken>>.Instance;
			_context = context ?? new AuthenticationTokenHandlerContext();
		}

		/// <inheritdoc/>
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			if (!ShouldIncludeToken(request))
			{
				// Request doesn't require an authentication token.
				_logger.LogDebug($"The request '{request.RequestUri}' doesn't require an authentication token.");

				return await base.SendAsync(request, ct);
			}

			var token = await _tokenProvider.GetToken(ct, request);

			var response = await SendWithAuthenticationToken(ct, request, token);

			if (!IsUnauthorized(request, response))
			{
				// Request was authorized, return the response.
				return response;
			}

			if (!token.CanBeRefreshed)
			{
				_logger.LogError($"The request '{request.RequestUri}' was unauthorized and the token '{token}' cannot be refreshed. Considering the session has expired.");

				// Request was unauthorized and we cannot refresh the authentication token.
				await TryNotifySessionExpired(ct, request, token);

				return response;
			}

			var refreshedToken = await RefreshAuthenticationToken(ct, request, token);

			if (refreshedToken == null)
			{
				_logger.LogError($"The request '{request.RequestUri}' was unauthorized and the token '{token}' could not be refreshed. Considering the session has expired.");

				// No authentication token to use.
				await TryNotifySessionExpired(ct, request, token);

				return response;
			}

			response = await SendWithAuthenticationToken(ct, request, refreshedToken);

			if (IsUnauthorized(request, response))
			{
				_logger.LogError($"The request '{request.RequestUri}' was unauthorized, the token '{token}' was refreshed to '{refreshedToken}' but the request was still unauthorized. Considering the session has expired.");

				// Request was still unauthorized and we cannot refresh the authentication token.
				await TryNotifySessionExpired(ct, request, refreshedToken);

				return response;
			}

			return response;

			async Task TryNotifySessionExpired(CancellationToken ct2, HttpRequestMessage innerRequest, TAuthenticationToken innerToken)
			{
				// Make sure that we notify that the session has been expired only once per TAutenticationToken.
				if (!innerToken.AccessToken.Equals(_context.LastExpiredToken))
				{
					_context.LastExpiredToken = innerToken.AccessToken;
					await _tokenProvider.NotifySessionExpired(ct2, innerRequest, innerToken);
				}
			}
		}

		/// <summary>
		/// Sends the <see cref="HttpRequestMessage"/> with the access token from
		/// the <paramref name="authenticationToken"/> or removes the Authorization
		/// header if there is no token.
		/// </summary>
		/// <param name="ct"><see cref="CancellationToken"/></param>
		/// <param name="request"><see cref="HttpRequestMessage"/></param>
		/// <param name="authenticationToken"><typeparamref name="TAuthenticationToken"/></param>
		/// <returns><see cref="HttpResponseMessage"/></returns>
		protected virtual async Task<HttpResponseMessage> SendWithAuthenticationToken(
			CancellationToken ct,
			HttpRequestMessage request,
			TAuthenticationToken authenticationToken
		)
		{
			if (authenticationToken?.AccessToken != null)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue(request.Headers.Authorization.Scheme, authenticationToken.AccessToken);
			}
			else
			{
				request.Headers.Remove("Authorization");
			}

			return await base.SendAsync(request, ct);
		}

		/// <summary>
		/// Attempts to refresh the authentication token.
		/// </summary>
		/// <param name="ct"><see cref="CancellationToken"/></param>
		/// <param name="request"><see cref="HttpRequestMessage"/></param>
		/// <param name="unauthorizedToken"><typeparamref name="TAuthenticationToken"/></param>
		/// <returns><typeparamref name="TAuthenticationToken"/></returns>
		protected virtual async Task<TAuthenticationToken> RefreshAuthenticationToken(
			CancellationToken ct,
			HttpRequestMessage request,
			TAuthenticationToken unauthorizedToken
		)
		{
			if (unauthorizedToken == null)
			{
				throw new ArgumentNullException(nameof(unauthorizedToken));
			}

			_logger.LogDebug($"Requesting refresh for token: '{unauthorizedToken}'.");

			// Wait for the semaphore.
			await _context.RefreshSemaphore.WaitAsync(ct);

			try
			{
				// From this moment, the operation cannot be cancelled.
				var refreshedToken = await GetRefreshedAuthenticationToken(CancellationToken.None);

				return refreshedToken;
			}
			finally
			{
				// Release the semaphore.
				_context.RefreshSemaphore.Release();
			}

			async Task<TAuthenticationToken> GetRefreshedAuthenticationToken(CancellationToken ct2)
			{
				// We get the current authentication token inside the lock
				// as it's very possible that the unauthorized token is no
				// longer the current token because another refresh request was made.
				var currentToken = await _tokenProvider.GetToken(ct2, request);

				_logger.LogDebug($"The current token is: '{currentToken}'.");

				// If we don't have an authentication data or a refresh token, we cannot refresh the access token.
				// This can happen if the session has expired while 2 concurrent refresh requests were made.
				// The second request will not have a refresh token.
				if (currentToken == null || !currentToken.CanBeRefreshed)
				{
					_logger.LogWarning($"The refresh token is null or cannot be refreshed.");

					return default;
				}

				// If we have an access token but it's not the same, the token has been refreshed.
				if (currentToken.AccessToken != null &&
					currentToken.AccessToken != unauthorizedToken.AccessToken)
				{
					_logger.LogWarning($"The access tokens are different. No need to refresh, returning the current token '{currentToken}'.");

					return currentToken;
				}

				try
				{
					_logger.LogDebug($"Refreshing token: '{unauthorizedToken}'.");

					var refreshedToken = await _tokenProvider.RefreshToken(ct2, request, currentToken);

					_logger.LogInformation($"Refreshed token: '{unauthorizedToken}' to '{refreshedToken}'.");

					return refreshedToken;
				}
				catch (Exception e)
				{
					_logger.LogError(e, $"Failed to refresh token: '{unauthorizedToken}'.");

					return default;
				}
			}
		}

		/// <summary>
		/// Returns whether or not the handler should include an authentication token.
		/// </summary>
		/// <param name="request"><see cref="HttpRequestMessage"/></param>
		/// <returns>Whether or not the request should include an authentication token</returns>
		public virtual bool ShouldIncludeToken(HttpRequestMessage request)
			=> request.Headers.Authorization != null;

		/// <summary>
		/// Returns whether or not the request is considered unauthorized.
		/// </summary>
		/// <param name="request"><see cref="HttpRequestMessage"/></param>
		/// <param name="response"><see cref="HttpResponseMessage"/></param>
		/// <returns>Whether or not the request is considered unauthorized</returns>
		public virtual bool IsUnauthorized(HttpRequestMessage request, HttpResponseMessage response)
			=> response.StatusCode == HttpStatusCode.Unauthorized;
	}
}
