using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MallardMessageHandlers
{
	/// <summary>
	/// This proxy ensures that only 1 RefreshToken operation runs a any time.
	/// It also ensures that only 1 NotifySessionExpired operation runs for equivalent expirations.
	/// </summary>
	/// <typeparam name="TAuthenticationToken">Type of authentication token</typeparam>
	public abstract class ConcurrentAuthenticationTokenProvider<TAuthenticationToken> : IAuthenticationTokenProvider<TAuthenticationToken>
		where TAuthenticationToken : IAuthenticationToken
	{
		private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
		private readonly ILogger _logger;

		private TAuthenticationToken _lastUnauthorizedToken;

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrentAuthenticationTokenProvider{TAuthenticationToken}"/> class.
		/// </summary>
		/// <param name="loggerFactory">The logger factory to create the internal logger for this class.</param>
		public ConcurrentAuthenticationTokenProvider(ILoggerFactory loggerFactory)
		{
			_logger = loggerFactory?.CreateLogger<ConcurrentAuthenticationTokenProvider<TAuthenticationToken>>() ?? NullLogger<ConcurrentAuthenticationTokenProvider<TAuthenticationToken>>.Instance;
		}

		/// <inheritdoc />
		public abstract Task<TAuthenticationToken> GetToken(CancellationToken ct, HttpRequestMessage request);

		/// <inheritdoc />
		public async Task NotifySessionExpired(CancellationToken ct, HttpRequestMessage request, TAuthenticationToken unauthorizedToken)
		{
			// Avoid notifiying more than once for the same token expiration.
			if (_lastUnauthorizedToken?.AccessToken != unauthorizedToken?.AccessToken)
			{
				_lastUnauthorizedToken = unauthorizedToken;
				await NotifySessionExpiredCore(ct, request, unauthorizedToken);
			}
		}

		/// <inheritdoc />
		public async Task<TAuthenticationToken> RefreshToken(CancellationToken ct, HttpRequestMessage request, TAuthenticationToken unauthorizedToken)
		{
			// Wait for other refresh operations to finish.
			await _semaphore.WaitAsync(ct);

			try
			{
				// From this moment, the operation cannot be cancelled.
				var refreshedToken = await GetRefreshedAuthenticationToken(CancellationToken.None);

				return refreshedToken;
			}
			finally
			{
				// Release the semaphore.
				_semaphore.Release();
			}

			async Task<TAuthenticationToken> GetRefreshedAuthenticationToken(CancellationToken ct2)
			{
				// We get the current authentication token inside the lock
				// as it's very possible that the unauthorized token is no
				// longer the current token because another refresh request was made.
				var currentToken = await GetToken(ct2, request);

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

					var refreshedToken = await RefreshTokenCore(ct2, request, currentToken);

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

		protected virtual Task NotifySessionExpiredCore(CancellationToken ct, HttpRequestMessage request, TAuthenticationToken unauthorizedToken)
		{
			return Task.CompletedTask;
		}

		protected abstract Task<TAuthenticationToken> RefreshTokenCore(CancellationToken ct, HttpRequestMessage request, TAuthenticationToken unauthorizedToken);
	}
}
