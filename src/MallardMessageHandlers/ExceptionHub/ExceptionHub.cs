using System;
using System.Collections.Generic;
using System.Text;

namespace MallardMessageHandlers
{
	public class ExceptionHub : IExceptionHub
	{
		/// <inheritdoc />
		public event EventHandler<Exception> OnExceptionReported;

		/// <inheritdoc />
		public void ReportException(Exception e)
		{
			OnExceptionReported?.Invoke(this, e);
		}
	}
}
