using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace MallardMessageHandlers.Tests
{
	public static class HttpClientTestsHelper
	{
		private const string TestHttpClientName = nameof(TestHttpClientName);

		public static IHttpClientFactory GetHttpClientFactory(Action<ServiceCollection> serviceCollectionBuilder)
		{
			var serviceProvider = GetServiceProvider(serviceCollectionBuilder);

			return serviceProvider.GetService<IHttpClientFactory>();
		}

		public static IServiceProvider GetServiceProvider(Action<ServiceCollection> serviceCollectionBuilder)
		{
			var serviceCollection = new ServiceCollection();

			serviceCollectionBuilder(serviceCollection);

			return serviceCollection.BuildServiceProvider();
		}

		public static HttpClient GetTestHttpClient(
			Action<ServiceCollection> serviceCollectionBuilder,
			Action<IHttpClientBuilder> httpClientBuilder
		)
		{
			var httpClientFactory = GetHttpClientFactory(s =>
			{
				serviceCollectionBuilder(s);

				httpClientBuilder(s.AddHttpClient(TestHttpClientName));
			});

			return httpClientFactory.CreateClient(TestHttpClientName);
		}

		public static HttpResponseMessage CreateHttpResponseMessage(TestResponse response, HttpStatusCode statusCode)
		{
			var serializedResult = JsonSerializer.Serialize(response);

			return new HttpResponseMessage()
			{
				Content = new StringContent(serializedResult),
				StatusCode = statusCode
			};
		}
	}
}
