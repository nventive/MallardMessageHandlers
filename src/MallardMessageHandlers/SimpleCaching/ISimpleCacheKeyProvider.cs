using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace MallardMessageHandlers.SimpleCaching;

/// <summary>
/// Provides a way to get a cache key from a <see cref="HttpRequestMessage"/>.
/// This is used by <see cref="SimpleCacheHandler"/>.
/// </summary>
public interface ISimpleCacheKeyProvider
{
	/// <summary>
	/// Gets the cache key for the <paramref name="request"/>.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>A cache key.</returns>
	string GetKey(HttpRequestMessage request);
}
