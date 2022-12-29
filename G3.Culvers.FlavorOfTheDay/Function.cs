using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using G3.Culvers.FlavorOfTheDay.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace G3.Culvers.FlavorOfTheDay;

public static class Function
{
    private static readonly IServiceProvider ServiceProvider;

    private const string HtmlTemplate = @"<html><head><meta charset='utf-8'><meta name='viewport'content='width=device-width,initial-scale=1'><link rel='icon'type='image/png'sizes='32x32'href='https://www.g3software.net/images/favicon-32x32.png?v=PYelMGByaz'><link rel='icon'type='image/png'sizes='16x16'href='https://www.g3software.net/images/favicon-16x16.png?v=PYelMGByaz'><title>{{Title}}</title><style>*{padding:0;margin:0}body{background-color:rgb(0,69,120);padding:20px}.store{width:300px;margin:0 auto 20px;text-align:center;background-color:#fff !important;border-radius:5px;filter:drop-shadow(2px 4px 6px black)}</style></head><body>{{Stores}}</body></html>";

    private const string StoreTemplate = @"<div class='store'><div class='title'>{{StoreName}}</div>{{Items}}</div>";

    private const string ItemTemplate = @"<div class='item'><div class='name'>{{Name}}</div><img src='{{ImageUrl}}'alt='{{Name}}'class='icon'/></div>";

    static Function()
    {
        var serviceCollection = new ServiceCollection()
            .AddLogging(x =>
                x
                    .SetMinimumLevel(LogLevel.Information)
                    .AddConsole());
        serviceCollection
            .AddHttpClient(
                "Default",
                x =>
                {
                    x.BaseAddress = new Uri("https://www.culvers.com", UriKind.Absolute);
                });
        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    /// <summary>
    /// The main entry point for the custom runtime.
    /// </summary>
    /// <param name="args"></param>
    private static async Task Main(
        string[] args)
    {
        var handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync();
    }

    public static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest input,
        ILambdaContext? context)
    {
        var httpClient = ServiceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("Default");
        var zip = input.QueryStringParameters["zip"];
        var restaurants = await httpClient.GetFromJsonAsync<StoresRoot>(
            $"/api/locate/address/json?address={zip}");
        var storeText = new StringBuilder();
        foreach (var restaurant in restaurants?.Collection?.Locations ?? Array.Empty<Location>())
        {
            storeText.Append(
                StoreTemplate
                    .Replace("{{StoreName}}", restaurant.Name)
                    .Replace("{{Items}}", ItemTemplate
                        .Replace("{{Name}}", restaurant.FlavorDay)
                        .Replace("{{ImageUrl}}", restaurant.FlavorImageUrl)));
        }

        return new APIGatewayHttpApiV2ProxyResponse
        {
            Body = HtmlTemplate
                .Replace(
                    "{{Title}}",
                    $"Flavors of the day for {zip}")
                .Replace(
                    "{{Stores}}",
                    storeText.ToString()),
            StatusCode = (int) HttpStatusCode.OK,
            Headers = new Dictionary<string, string>
            {
                {"Content-Type", "text/html"},
                {"Access-Control-Allow-Origin", "*"},
                {"Access-Control-Allow-Credentials", "true"}
            }
        };
    }
}