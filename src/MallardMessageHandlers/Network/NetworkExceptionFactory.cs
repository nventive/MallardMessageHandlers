using System;
using System.Collections.Generic;
using System.Text;

namespace MallardMessageHandlers
{
	public class NetworkExceptionFactory : INetworkExceptionFactory
	{
		public Exception CreateNetworkException(Exception innerException)
		{
			return new NoNetworkException("There is no network", innerException);
		}
	}
}
