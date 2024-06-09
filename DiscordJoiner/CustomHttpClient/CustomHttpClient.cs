using System.Net.Http.Headers;
using System.Security.Authentication;

namespace DiscordJoiner;

public class CustomHttpClient
{
    private readonly HttpClient _client;

    public CustomHttpClient()
    {
        _client = new(new HttpClientHandler()
        {
            SslProtocols = SslProtocols.Tls13,
            DefaultProxyCredentials = null,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
        })
        {
            DefaultRequestVersion = new(2, 0)
        };
    }

    public HttpResponseMessage? Post(string url, string data, List<string> headers)
    {
        HttpRequestMessage request = new(HttpMethod.Post, url)
        {
            Content = new StringContent(data, new MediaTypeHeaderValue("application/json"))
        };

        foreach(var header in headers)
        {
            string[] split = header.Split(": ", 2);
            request.Headers.Add(split[0], split[1]);
        }

        var response = _client.Send(request);

        return response;
    }
}
