namespace Perfx
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            // Credit: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-3.1
            // Credit: https://thecodebuzz.com/using-httpclientfactory-in-net-core-console-application/
            var builder = Host
                            .CreateDefaultBuilder(args)
                            .ConfigureServices((hostContext, services) =>
                            {
                                services
                                    .AddScoped<PerfRunner>()
                                    .AddHostedService<WorkerService>()
                                    //.AddTransient<TimingHandler>()
                                    .AddHttpClient(nameof(Perfx)); //.AddHttpMessageHandler<TimingHandler>();
                            })
                            .ConfigureLogging((hostContext, logging) =>
                            {
                                logging.ClearProviders()
                                    .SetMinimumLevel(LogLevel.Warning)
                                    //.AddFilter("System.Net.Http.HttpClient", LogLevel.Information)
                                    .AddConsole(c => c.IncludeScopes = true);
                            })
                            .UseConsoleLifetime();

            await builder.RunConsoleAsync();
        }
    }
}
