using System;
using System.Collections.Generic;
using System.Text;

namespace MallardMessageHandlers
{
    public class NoNetworkException : Exception
    {
        public NoNetworkException()
        {
        }

        public NoNetworkException(string message)
			: base(message)
        {
        }

        public NoNetworkException(string message, Exception innerException)
			: base(message, innerException)
        {
        }
    }
}
