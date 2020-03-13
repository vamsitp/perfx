namespace Perfx
{
    using System;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;

    public class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            // Credit: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-3.1
            // Credit: https://thecodebuzz.com/using-httpclientfactory-in-net-core-console-application/
            Console.OutputEncoding = Encoding.UTF8;
            IConfiguration settings = null;
            var builder = Host
                            .CreateDefaultBuilder(args)
                            .ConfigureAppConfiguration((hostContext, config) =>
                            {
                                config.AddJsonFile(Settings.AppSettingsFile, optional: true, reloadOnChange: true);
                                config.AddJsonFile(Settings.DefaultSettingsFile, optional: true, reloadOnChange: true);
                                settings = config.Build();
                            })
                            .ConfigureServices((hostContext, services) =>
                            {
                                services
                                    .Configure<Settings>(settings)
                                    .AddScoped<PerfRunner>()
                                    .AddHostedService<Worker>()
                                    .AddSingleton<LogDataService>()
                                    .AddTransient<TimingHandler>()
                                    .AddSingleton<JsonSerializer>()
                                    .AddHttpClient(nameof(Perfx))
                                    .AddHttpMessageHandler<TimingHandler>();
                            })
                            .ConfigureLogging((hostContext, logging) =>
                            {
                                logging
                                .ClearProviders()
                                .SetMinimumLevel(LogLevel.Warning)
                                .AddDebug()
                                .AddConsole();
                            })
                            .UseConsoleLifetime();
            await builder.RunConsoleAsync();
        }
    }
}
