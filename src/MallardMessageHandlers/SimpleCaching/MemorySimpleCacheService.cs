using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MallardMessageHandlers.SimpleCaching;

/// <summary>
/// This is a simple implementation of <see cref="ISimpleCacheService"/> that stores payloads in a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// and uses <see cref="DateTimeOffset.Now"/> for time reference.
/// </summary>
[Preserve(AllMembers = true)]
public sealed class MemorySimpleCacheService : ISimpleCacheService
{
	private static readonly Task<bool> _trueResult = Task.FromResult(true);
	private static readonly Task<bool> _falseResult = Task.FromResult(false);
	private readonly ConcurrentDictionary<string, CacheEntry> _data = new ConcurrentDictionary<string, CacheEntry>();

	/// <inheritdoc />
	public Task Add(string key, byte[] payload, TimeSpan timeToLive, CancellationToken ct)
	{
		if (ct.IsCancellationRequested)
		{
			return Task.CompletedTask;
		}

		var entry = new CacheEntry(payload, DateTimeOffset.Now + timeToLive);
		_data[key] = entry;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task Clear(CancellationToken ct)
	{
		if (ct.IsCancellationRequested)
		{
			return Task.CompletedTask;
		}

		_data.Clear();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> TryGet(string key, CancellationToken ct, out byte[] payload)
	{
		if (ct.IsCancellationRequested)
		{
			payload = null;
			return _falseResult;
		}

		if (_data.TryGetValue(key, out var entry))
		{
			if (entry.Expiration > DateTimeOffset.Now)
			{
				payload = entry.Payload;
				return _trueResult;
			}
			else
			{
				_data.TryRemove(key, out _);
			}
		}

		payload = null;
		return _falseResult;
	}

	private record CacheEntry(byte[] Payload, DateTimeOffset Expiration) { }
}
