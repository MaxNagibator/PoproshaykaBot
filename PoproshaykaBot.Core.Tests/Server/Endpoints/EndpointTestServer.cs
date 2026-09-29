using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PoproshaykaBot.Core.Server.Endpoints;

namespace PoproshaykaBot.Core.Tests.Server.Endpoints;

internal sealed class EndpointTestServer : IDisposable
{
    private readonly IHost _host;

    private EndpointTestServer(IHost host)
    {
        _host = host;
    }

    public TestServer Server => _host.GetTestServer();

    public static async Task<EndpointTestServer> CreateAsync(
        Action<IServiceCollection> configureServices,
        Func<IServiceProvider, IEndpointMapper> mapperFactory)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer()
                    .ConfigureServices(configureServices)
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            var mapper = mapperFactory(app.ApplicationServices);
                            mapper.Map(endpoints);
                        });
                    });
            });

        var host = await builder.StartAsync();
        return new(host);
    }

    public const string MatchedEndpointHeader = "X-Matched-Endpoint";

    public static async Task<EndpointTestServer> CreateAllAsync(Action<IServiceCollection> configureServices)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer()
                    .ConfigureServices(configureServices)
                    .Configure(app =>
                    {
                        app.UseRouting();

                        app.Use(async (ctx, next) =>
                        {
                            var matched = ctx.GetEndpoint();

                            if (matched != null)
                            {
                                ctx.Response.Headers[MatchedEndpointHeader] = matched.DisplayName;
                            }

                            await next(ctx);
                        });

                        app.UseEndpoints(endpoints =>
                        {
                            foreach (var mapper in app.ApplicationServices.GetRequiredService<IEnumerable<IEndpointMapper>>())
                            {
                                mapper.Map(endpoints);
                            }
                        });
                    });
            });

        var host = await builder.StartAsync();
        return new(host);
    }

    public HttpClient CreateClient()
    {
        return _host.GetTestClient();
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}
