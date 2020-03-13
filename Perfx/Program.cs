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
                                config.AddJsonFile(Utils.SettingsFile, optional: true, reloadOnChange: true);
                                settings = config.Build();
                            })
                            .ConfigureServices((hostContext, services) =>
                            {
                                services
                                    .Configure<Settings>(settings)
                                    .AddScoped<PerfRunner>()
                                    .AddHostedService<WorkerService>()
                                    .AddSingleton<LogDataService>()
                                    .AddTransient<TimingHandler>()
                                    .AddSingleton<JsonSerializer>()
                                    .AddHttpClient(nameof(Perfx))
                                    .AddHttpMessageHandler<TimingHandler>();
                            })
                            //.ConfigureLogging((hostContext, logging) =>
                            //{
                            //    logging.ClearProviders()
                            //        .AddDebug();
                            //        //.AddConsole(c => c.IncludeScopes = true)
                            //        //.SetMinimumLevel(LogLevel.Warning);
                            //})
                            .UseConsoleLifetime();
            await builder.RunConsoleAsync();
        }
    }
}
