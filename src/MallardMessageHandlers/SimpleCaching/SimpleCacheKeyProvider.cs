using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace MallardMessageHandlers.SimpleCaching;

/// <summary>
/// This class provides a default implementation of <see cref="ISimpleCacheKeyProvider"/> and common static instances.
/// </summary>
public sealed class SimpleCacheKeyProvider : ISimpleCacheKeyProvider
{
	/// <summary>
	/// Gets a <see cref="ISimpleCacheKeyProvider"/> that generates a cache key using only the Uri of the <see cref="HttpRequestMessage"/>.
	/// </summary>
	public static ISimpleCacheKeyProvider FromUriOnly { get; } = new SimpleCacheKeyProvider(GetKeyFromUriOnly);

	/// <summary>
	/// Gets a <see cref="ISimpleCacheKeyProvider"/> that generates a cache key using the Uri of the <see cref="HttpRequestMessage"/> and a hash (SHA256) of the Authorization header value.
	/// </summary>
	public static ISimpleCacheKeyProvider FromUriAndAuthorizationHash { get; } = new UriAndAuthorizationHashCacheKeyProvider();

	private static string GetKeyFromUriOnly(HttpRequestMessage httpRequestMessage)
		=> httpRequestMessage.RequestUri.ToString();

	private readonly Func<HttpRequestMessage, string> _function;

	/// <summary>
	/// Initializes a new instances of <see cref="SimpleCacheKeyProvider"/>.
	/// </summary>
	/// <param name="function">The function that gets the cache key.</param>
	public SimpleCacheKeyProvider(Func<HttpRequestMessage, string> function)
	{
		_function = function;
	}

	/// <inheritdoc />
	public string GetKey(HttpRequestMessage request) => _function(request);
}

/// <summary>
/// This implementation of <see cref="ISimpleCacheKeyProvider"/> generates a cache key using the Uri of the <see cref="HttpRequestMessage"/> and a hash of the Authorization header value.
/// </summary>
public sealed class UriAndAuthorizationHashCacheKeyProvider : ISimpleCacheKeyProvider, IDisposable
{
	private readonly System.Security.Cryptography.SHA256Managed _sha = new();

	/// <inheritdoc/>
	public string GetKey(HttpRequestMessage httpRequestMessage)
	{
		var sb = new StringBuilder();
		sb.Append(httpRequestMessage.RequestUri);
		var authorizationValue = httpRequestMessage.Headers.Authorization.Parameter;
		if (!string.IsNullOrEmpty(authorizationValue))
		{
			var textData = Encoding.UTF8.GetBytes(authorizationValue);
			var hashBytes = _sha.ComputeHash(textData);
			var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
			sb.Append(hash);
		}
		return sb.ToString();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_sha.Dispose();
	}
}
