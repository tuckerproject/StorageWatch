using System.Net;
using System.Net.Http;
using System.Text;

namespace StorageWatch.Updater.Tests.Helpers;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responseFactory(request));
    }

    public static FakeHttpMessageHandler FromBytes(string url, byte[] content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new FakeHttpMessageHandler(req =>
        {
            if (string.Equals(req.RequestUri?.ToString(), url, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(statusCode)
                {
                    Content = new ByteArrayContent(content)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not found", Encoding.UTF8)
            };
        });
    }

    public static FakeHttpMessageHandler FromJson(string url, string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new FakeHttpMessageHandler(req =>
        {
            if (string.Equals(req.RequestUri?.ToString(), url, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not found", Encoding.UTF8)
            };
        });
    }
}
