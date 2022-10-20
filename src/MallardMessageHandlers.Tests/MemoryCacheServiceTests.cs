using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MallardMessageHandlers.SimpleCaching;
using Moq;
using Xunit;

namespace MallardMessageHandlers.Tests
{
	public class MemoryCacheServiceTests
	{
		private readonly CancellationToken _ctNone = CancellationToken.None;

		[Fact]
		public async Task TryGet_returns_non_expired_payloads()
		{
			// Arrange
			var cache = new MemorySimpleCacheService();
			var payload = new byte[0];

			// Act
			await cache.Add("key", payload, TimeSpan.FromMinutes(1000), _ctNone);

			// Assert
			(await cache.TryGet("key", _ctNone, out var content)).Should().BeTrue();
			content.Should().BeSameAs(payload);
		}

		[Fact]
		public async Task TryGet_doesnt_return_expired_payloads()
		{
			// Arrange
			var cache = new MemorySimpleCacheService();
			var payload = new byte[0];
			
			// Act
			await cache.Add("key", payload, TimeSpan.FromMinutes(0), _ctNone);

			// Assert
			(await cache.TryGet("key", _ctNone, out var content)).Should().BeFalse();
			content.Should().BeNull();
		}

		[Fact]
		public async Task Clear_removes_previous_items()
		{
			// Arrange
			var cache = new MemorySimpleCacheService();
			var payload = new byte[0];

			// Act
			await cache.Add("key", payload, TimeSpan.FromMinutes(1000), _ctNone);
			await cache.Clear(_ctNone);

			// Assert
			(await cache.TryGet("key", _ctNone, out var content)).Should().BeFalse();
			content.Should().BeNull();
		}

		[Fact]
		public async Task Add_preserves_the_latest_value()
		{
			// Arrange
			var cache = new MemorySimpleCacheService();
			var payload = new byte[0];
			var payload2 = new byte[1];

			// Act
			await cache.Add("key", payload, TimeSpan.FromMinutes(1000), _ctNone);
			await cache.Add("key", payload2, TimeSpan.FromMinutes(1000), _ctNone);

			// Assert
			(await cache.TryGet("key", _ctNone, out var content)).Should().BeTrue();
			content.Should().BeSameAs(payload2);
		}
		
	}
}
