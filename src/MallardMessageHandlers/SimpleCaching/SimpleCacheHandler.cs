using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MallardMessageHandlers.SimpleCaching;

[Preserve(AllMembers = true)]
public sealed class SimpleCacheHandler : DelegatingHandler
{
	/// <summary>
	/// The HTTP header name for the Time-To-Live caching instruction.
	/// The header value needs to be an integer representing the total seconds of the time-to-live.
	/// <code>
	/// // Example:
	/// "X-Mallard-SimpleCache-TTL:600" // Sets the time-to-live to 10 minutes (600 seconds).
	/// </code>
	/// </summary>
	public const string CacheTimeToLiveHeaderName = "X-Mallard-SimpleCache-TTL";

	/// <summary>
	/// The HTTP header name for the Force-Refresh caching instruction.
	/// The header value needs to be a boolean representing whether the force-refresh instruction is to be applied.
	/// </summary>
	public const string CacheForceRefreshHeaderName = "X-Mallard-SimpleCache-ForceRefresh";

	/// <summary>
	/// The HTTP header name for the Disable caching instruction.
	/// The header value needs to be a boolean representing whether the disable instruction is to be applied.
	/// This instruction takes precedence over all others.
	/// </summary>
	public const string CacheDisableHeaderName = "X-Mallard-SimpleCache-Disable";

	private readonly ISimpleCacheService _cacheService;
	private readonly ISimpleCacheKeyProvider _cacheKeyProvider;

	/// <summary>
	/// Initializes a new instance of <see cref="SimpleCacheHandler"/>.
	/// </summary>
	/// <param name="cacheService">The cache service to use to store payloads.</param>
	/// <param name="cacheKeyProvider">The <see cref="ISimpleCacheKeyProvider"/> to use to generate the cache keys.</param>
	public SimpleCacheHandler(ISimpleCacheService cacheService, ISimpleCacheKeyProvider cacheKeyProvider)
	{
		_cacheService = cacheService;
		_cacheKeyProvider = cacheKeyProvider;
	}

	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (request.Method != HttpMethod.Get)
		{
			return await base.SendAsync(request, cancellationToken);
		}

		// The Cache-Disable header overrides all other cache instructions and forces normal execution.
		if (ExtractIsCacheDisabled(request))
		{
			return await base.SendAsync(request, cancellationToken);
		}

		var cacheKey = _cacheKeyProvider.GetKey(request);
		var isForceRefresh = ExtractIsForceRefresh(request);
		var isCacheable = ExtractIsCacheEnabled(request, out var timeToLive);
		var cachedPayload = default(byte[]);

		if (isForceRefresh || !isCacheable || isCacheable && !await _cacheService.TryGet(cacheKey, cancellationToken, out cachedPayload))
		{
			var response = await base.SendAsync(request, cancellationToken);

			if (isCacheable && response.IsSuccessStatusCode && !cancellationToken.IsCancellationRequested)
			{
				await _cacheService.Add(cacheKey, await response.Content.ReadAsByteArrayAsync(), timeToLive, cancellationToken);
			}

			return response;
		}
		else
		{
			var cacheResponse = new HttpResponseMessage();
			cacheResponse.Content = new ByteArrayContent(cachedPayload);
			return cacheResponse;
		}
	}

	/// <summary>
	/// Gets whether the request is a force refresh request and removes the force refresh header.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>Whether <paramref name="request"/> requires a refresh.</returns>
	private bool ExtractIsForceRefresh(HttpRequestMessage request)
	{
		var headerName = CacheForceRefreshHeaderName;
		if (request.Headers.TryGetValues(headerName, out var forceRefreshValues))
		{
			request.Headers.Remove(headerName);
			var rawForceRefresh = forceRefreshValues.LastOrDefault();
			return bool.Parse(rawForceRefresh);
		}

		return false;
	}

	/// <summary>
	/// Gets whether the request has caching disabled.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>Whether <paramref name="request"/> doesn't have caching.</returns>
	private bool ExtractIsCacheDisabled(HttpRequestMessage request)
	{
		var headerName = CacheDisableHeaderName;
		if (request.Headers.TryGetValues(headerName, out var disableCacheValues))
		{
			request.Headers.Remove(headerName);
			request.Headers.Remove(CacheForceRefreshHeaderName);
			request.Headers.Remove(CacheTimeToLiveHeaderName);
			var rawDisableCache = disableCacheValues.LastOrDefault();
			return bool.Parse(rawDisableCache);
		}

		return false;
	}

	/// <summary>
	/// Gets whether the request is a force refresh request and removes the force refresh header.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="timeToLive">The time to live of result of this request, when the cache is enable.</param>
	/// <returns>Whether <paramref name="request"/> requires a refresh.</returns>
	private bool ExtractIsCacheEnabled(HttpRequestMessage request, out TimeSpan timeToLive)
	{
		var headerName = CacheTimeToLiveHeaderName;
		if (request.Headers.TryGetValues(headerName, out var timeToLiveValues))
		{
			request.Headers.Remove(headerName);
			// We use LastOrDefault to use the lastest value. (The can be a default value and an override value).
			var rawTimeToLive = timeToLiveValues.LastOrDefault();
			timeToLive = TimeSpan.FromSeconds(int.Parse(rawTimeToLive));
			return true;
		}

		return false;
	}
}
