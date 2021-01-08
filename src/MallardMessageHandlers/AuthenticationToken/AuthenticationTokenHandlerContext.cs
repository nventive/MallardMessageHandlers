using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MallardMessageHandlers
{
	public class AuthenticationTokenHandlerContext
	{
		public AuthenticationTokenHandlerContext()
		{
			RefreshSemaphore = new SemaphoreSlim(1);
		}

		public SemaphoreSlim RefreshSemaphore { get; private set; }

		public string LastExpiredToken { get; set; }
	}
}
