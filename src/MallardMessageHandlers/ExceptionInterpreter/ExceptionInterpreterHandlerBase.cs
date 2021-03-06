﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MallardMessageHandlers
{
	/// <summary>
	/// This <see cref="HttpMessageHandler"/> interprets the request / response
	/// in order to throw a specific type of exception. Users of this handler
	/// should implement the <see cref="InterpretException" /> method.
	/// </summary>
	public abstract class ExceptionInterpreterHandlerBase : DelegatingHandler
	{
		/// <inheritdoc/>
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			var response = await base.SendAsync(request, ct);

			if (response.IsSuccessStatusCode)
			{
				return response;
			}

			var exception = await InterpretException(ct, request, response);

			if (exception != null)
			{
				throw exception;
			}

			return response;
		}

		/// <summary>
		/// Interprets an http request / message in order to get a specific type of exception.
		/// This method will be called if the response doesn't have a sucess status code.
		/// If null is returned, this HttpMessageHandler will not throw an exception.
		/// </summary>
		/// <param name="ct">Cancellation token</param>
		/// <param name="request">Http request</param>
		/// <param name="response">Http response</param>
		/// <returns>Exception if interpreted. Null otherwise.</returns>
		protected abstract Task<Exception> InterpretException(CancellationToken ct, HttpRequestMessage request, HttpResponseMessage response);
	}
}
