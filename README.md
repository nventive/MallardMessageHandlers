# MallardMessageHandlers

`MallardMessageHandlers` offers `DelegatingHandlers` which will be handy in many projects which use the HTTP stack.

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

## Getting Started

Add to your project a reference to the `MallardMessageHandlers` nuget package.

`DelegatingHandlers` are decorators of `HttpMessageHandler`. They are used to add logic to `HttpRequests` **before** their execution and **after** their execution. You would generally create a pipeline of `DelegatingHandlers` to send and interpret `HttpRequests`.

```
HttpClient -> Handler1 -> Handler2 -> Handler3 -> (...before)
                                                               Network
HttpClient <- Handler1 <- Handler2 <- Handler3 <- (...after)
```

You can create a pipeline of `DelegatingHandlers` using the `IHttpClientFactory`. 

**The order is extremely important as it will define the sequence of execution**.

The following example shows how you would create the above pipeline.

```csharp
public IHttpClientBuilder ConfigureMyEndpoint(IServiceCollection services)
{
  return services
    .AddHttpClient("MyHttpClient")
    .AddHttpMessageHandler<Handler1>()
    .AddHttpMessageHandler<Handler2>()
    .AddHttpMessageHandler<Handler3>()
}
```

This repository contains multiple implementations of `DelegatingHandlers` for different purposes. 

Here is a list of the `DelegatingHandlers` provided.

- [NetworkExceptionHandler](#NetworkExceptionHandler) : Throws an exception if the HttpRequest fails and there is no network.
- [ExceptionHubHandler](#ExceptionHubHandler) : Reports all exceptions that occur on the pipeline.
- [ExceptionInterpreterHandler](#ExceptionInterpreterHandler) : Interprets error responses and converts them to exceptions.
- [AuthenticationTokenHandler](#AuthenticationTokenHandler) : Adds the authentication token to the authorization header.

You can find official documentation on using delegating handlers here: https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/http-message-handlers.

## Features

### NetworkExceptionHandler

The `NetworkExceptionHandler` is a `DelegatingHandler` that throws a specific type of exception if an exception occurs during the `HttpRequest` execution and the network availability check returns false.

By default, the `NetworkExceptionHandler` will throw a `NoNetworkException`.

To create a `NetworkExceptionHandler`, you provide a delegate that will check the network availability.

_Note: The network availability check will only be called **if an exception occurs** during the `HttpRequest` execution._

```csharp
var networkAvailabilityChecker = new NetworkAvailabilityChecker(GetIsNetworkAvailable);
var networkExceptionHandler = new NetworkExceptionHandler(networkAvailabilityChecker);

private Task<bool> GetIsNetworkAvailable(CancellationToken ct)
{
  // Add your network connectivity check here.
}
```

You can set your own type of exception returned by the handler by implementing  `INetworkExceptionFactory`.

```csharp
var exceptionFactory = new MyNetworkExceptionFactory();
var handler = new NetworkExceptionHandler(networkAvailabilityChecker, exceptionFactory);

private class MyNetworkExceptionFactory : INetworkExceptionFactory
{
  ...
}
```

You would generally register the `NetworkExceptionHandler` on your `IServiceProvider`.

```csharp
private void ConfigureNetworkExceptionHandler(IServiceCollection services)
{
  // The NetworkAvailabilityChecker must be shared for all HttpRequests so we add it as singleton.
  services.AddSingleton<INetworkAvailabilityChecker>(s => new NetworkAvailabilityChecker(GetIsNetworkAvailable));

  // The NetworkExceptionHandler must be recreated for all HttpRequests so we add it as transient.
  services.AddTransient<NetworkExceptionHandler>();
}
```

### ExceptionHubHandler

The `ExceptionHubHandler` is a `DelegatingHandler` that will report all exceptions thrown during the execution of the `HttpRequest` to an `IExceptionHub`.

To create a `ExceptionHubHandler`, you provide a `IExceptionHub` that you will use to receive the exceptions.

```csharp
var exceptionHub = new ExceptionHub();
var handler = new exceptionHubHandler(exceptionHub);

exceptionHub.OnExceptionReported += OnExceptionReported;

void OnExceptionReported(object sender, Exception e)
{
  // This will be called everytime an exception occurs during the execution of the HttpRequests.
}
```

You would generally register the `ExceptionHubHandler` on your `IServiceProvider`.

```csharp
private void ConfigureExceptionHubHandler(IServiceCollection services)
{
  // The ExceptionHub must be shared for all HttpRequests so we add it as singleton.
  services.AddSingleton<IExceptionHub, ExceptionHub>();

  // The ExceptionHubHandler must be recreated for all HttpRequests so we add it as transient.
  services.AddTransient<ExceptionHubHandler>();
}
```

### ExceptionInterpreterHandler

The `ExceptionInterpreterHandler` is a `DelegatingHandler` that will interpret the response of an `HttpRequest` and throw a specific type of exception if the response is considered in error.

To create a `ExceptionInterpreterHandler`, you provide a response interpreter and a deserializer.

```csharp
var interpreter = new ErrorResponseInterpreter<TestResponse>(
  // Whether or not this interpreter should throw an exception.
  (request, response, deserializedResponse) => deserializedResponse.Error != null, 

  // The exception that should be thrown.
  (request, response, deserializedResponse) => new TestException(deserializedResponse.Error) 
);

// Use your response deserializer.
var deserializer = new ResponseContentDeserializer();

var handler = new ExceptionInterpreterHandler<TestResponse>(interpreter, deserializer);
```

You would generally register the `ExceptionInterpreterHandler` on your `IServiceProvider`.

```csharp
private void ConfigureExceptionInterpreterHandler(IServiceCollection services)
{
  // The ResponseContentDeserializer must be shared for all HttpRequests so we add it as singleton.
  services.AddSingleton<IResponseContentDeserializer, ResponseContentDeserializer>();

  // The ErrorResponseInterpreter must be shared for all HttpRequests so we add it as singleton.
  services.AddSingleton<IErrorResponseInterpreter<TestResponse>>(s => ...);

  // The ExceptionInterpreterHandler must be recreated for all HttpRequests so we add it as transient.
  services.AddTransient<ExceptionInterpreterHandler<TestResponse>>();
}
```

### AuthenticationTokenHandler

The `AuthenticationTokenHandler` is a `DelegatingHandler` that will add the value of the authentication token to the `Authorization` header if the header is present. It will also refresh the token if possible and notify if the authenticated session should be considered as expired.

For example, if you have a Refit endpoint with the following header, the authentication token will automatically be added to the `Authorization` header of the `HttpRequest`.

```csharp
// This adds the Authorization header to all API calls of this endpoint.
[Headers("Authorization: Bearer")]
public interface IMyEndpoint
{
  [Get("/categories")]
  Task<Category[]> GetCategories(CancellationToken ct);
}
```

To create a `AuthenticationTokenHandler`, you provide an `IAuthenticationTokenProvider`.

There is an implementation of `IAuthenticationTokenProvider` that receives the different delegates as parameters but you can create your own implementation.

```csharp
var authenticationService = new AuthenticationService();

var authenticationTokenProvider = new AuthenticationTokenProvider<MyAuthenticationToken>(
  getToken: (ct, request) => authenticationService.GetToken(ct, request),
  notifySessionExpired: (ct, request, token) => authenticationService.NotifySessionExpired(ct, request, token),
  refreshToken: (ct, request, token) => authenticationService.RefreshToken(ct, request, token)  // Optional
);

var authenticationHandler = new AuthenticationTokenHandler<MyAuthenticationToken>(authenticationTokenProvider);

public class MyAuthenticationToken : IAuthenticationToken
{
  public string AccessToken { get; set; } // Access token used for the header.

  public string RefreshToken { get; set; } // Refresh token used to refresh the access token.

  public bool CanBeRefreshed => RefreshToken != null; // Whether or not the access token can be refreshed.
}

public class MyAuthenticationService
{
  public Task<MyAuthenticationToken> GetToken(CancellationToken ct, HttpRequestMessage request)
  {
    // Return the authentication token from your app settings.
  }

  public Task<MyAuthenticationToken> RefreshToken(CancellationToken ct, HttpRequestMessage request, MyAuthenticationToken unauthorizedToken)
  {
    // Refresh the authentication token with your API.
  }

  public Task NotifySessionExpired(CancellationToken ct, HttpRequestMessage request, MyAuthenticationToken unauthorizedToken)
  {
    // This will occur if the token is expired and it couldn't be refreshed.
    // This should generally result in a user logout.
  }
}
```

You would generally register the `AuthenticationTokenHandler` on your `IServiceProvider`.

```csharp
private void ConfigureAuthenticationTokenHandler(IServiceCollection services)
{
  // The AuthenticationTokenHandlerContext must be shared for all HttpRequests, so we add it as singleton.
  services.AddSingleton<AuthenticationTokenHandlerContext>();

  // The AuthenticationTokenProvider must be shared for all HttpRequests so we add it as singleton.
  services.AddSingleton<IAuthenticationTokenProvider<MyAuthenticationToken>, MyAuthenticationTokenProvider>();

  // The AuthenticationTokenHandler must be recreated for all HttpRequests so we add it as transient.
  services.AddTransient<AuthenticationTokenHandler<MyAuthenticationToken>>();
}
```

## Changelog

Please consult the [CHANGELOG](CHANGELOG.md) for more information about version
history.

## License

This project is licensed under the Apache 2.0 license - see the
[LICENSE](LICENSE) file for details.

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on the process for
contributing to this project.

Be mindful of our [Code of Conduct](CODE_OF_CONDUCT.md).

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- ALL-CONTRIBUTORS-LIST:END -->
