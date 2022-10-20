using System;
using System.Collections.Generic;
using System.Text;
using Refit;

namespace MallardMessageHandlers.SimpleCaching
{
	/// <summary>
	/// Sets the cache time-to-live for the current call.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
	public class TimeToLiveAttribute : HeadersAttribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TimeToLiveAttribute"/> class.
		/// </summary>
		/// <param name="totalSeconds">The time-to-live in seconds.</param>
		public TimeToLiveAttribute(int totalSeconds)
			: base(SimpleCacheHandler.CacheTimeToLiveHeaderName + ":" + totalSeconds)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TimeToLiveAttribute"/> class.
		/// </summary>
		/// <param name="totalMinutes">The time-to-live in minutes.</param>
		public TimeToLiveAttribute(double totalMinutes)
			: base(SimpleCacheHandler.CacheTimeToLiveHeaderName + ":" + (int)(totalMinutes * 60))
		{
		}
	}

	/// <summary>
	/// Bypasses the other simple caching instructions.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
	public class NoCacheAttribute : HeadersAttribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NoCacheAttribute"/> class.
		/// </summary>
		public NoCacheAttribute()
			: base(SimpleCacheHandler.CacheDisableHeaderName + ":true")
		{
		}
	}

	/// <summary>
	/// Associates the force-refresh simple caching instruction with a boolean parameter.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	public class ForceRefresh : HeaderAttribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ForceRefresh"/> class.
		/// </summary>
		public ForceRefresh()
			: base(SimpleCacheHandler.CacheForceRefreshHeaderName)
		{
		}
	}
}
