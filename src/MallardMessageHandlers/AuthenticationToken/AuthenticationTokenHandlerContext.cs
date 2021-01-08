using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MallardMessageHandlers
{
	/// <summary>
	/// This represents the context for a <see cref="AuthenticationTokenHandler{TAuthenticationToken}"/>.
	/// It's use mostly to help <see cref="AuthenticationTokenHandler{TAuthenticationToken}"/> handles thoses actions:
	/// - Refreshing the token when expired.
	/// - Detecting an expiration session.
	/// By default, each <see cref="AuthenticationTokenHandler{TAuthenticationToken}"/> has its own context.
	/// However, if there's multiple <see cref="AuthenticationTokenHandler{TAuthenticationToken}"/> (e.g. multiple endpoints),
	/// it's strongly recommended that they're all shared the same context,
	/// otherwise there's going to be synchronization issues when one of the endpoint either refresh the TAuthenticationToken or signal an expired session.
	/// </summary>
	public class AuthenticationTokenHandlerContext
	{
		public AuthenticationTokenHandlerContext()
		{
			RefreshSemaphore = new SemaphoreSlim(1);
		}

		public SemaphoreSlim RefreshSemaphore { get; private set; }

		public string LastExpiredToken { get; internal set; }
	}
}
