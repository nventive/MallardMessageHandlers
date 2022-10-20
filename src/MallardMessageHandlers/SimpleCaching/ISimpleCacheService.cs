using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MallardMessageHandlers.SimpleCaching;

/// <summary>
/// Represents a simple cache service allowing to cache data using a string key and a time-to-live.
/// </summary>
public interface ISimpleCacheService
{
	/// <summary>
	/// Adds a payload to the cache with the specified key and expiration.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="payload">The data to cache.</param>
	/// <param name="timeToLive">The duration for which the cache entry should stay retrievable.</param>
	/// <param name="ct">The cancellation token.</param>
	Task Add(string key, byte[] payload, TimeSpan timeToLive, CancellationToken ct);

	/// <summary>
	/// Gets the payload associated with the specified key, if available.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="ct">The cancellation token.</param>
	/// <param name="payload">The cache content for that key.</param>
	/// <returns>The cached payload or null when the key isn't found or when the payload is expired.</returns>
	Task<bool> TryGet(string key, CancellationToken ct, out byte[] payload);

	/// <summary>
	/// Clears all cache data.
	/// </summary>
	/// <param name="ct">The cancellation token.</param>
	Task Clear(CancellationToken ct);
}
