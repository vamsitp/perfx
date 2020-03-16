﻿namespace Perfx
{
    using System;
    using System.Text;
    using System.Threading.Tasks;

    using ColoredConsole;

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
            IConfiguration configuration = null;
            var builder = Host
                            .CreateDefaultBuilder(args)
                            //.ConfigureHostConfiguration(configHost => { })
                            .ConfigureAppConfiguration((hostContext, config) =>
                            {
                                config.AddJsonFile(Settings.AppSettingsFile, optional: true, reloadOnChange: true);
                                config.AddJsonFile(Settings.DefaultSettingsFile, optional: true, reloadOnChange: true);
                                configuration = config.Build();
                            })
                            .ConfigureServices((hostContext, services) =>
                            {
                                services
                                    .Configure<Settings>(configuration)
                                    .AddScoped<PerfRunner>()
                                    .AddHostedService<Worker>()
                                    .AddSingleton<LogDataService>()
                                    .AddTransient<TimingHandler>()
                                    .AddSingleton<JsonSerializer>()
                                    .AddHttpClient(nameof(Perfx))
                                    .AddHttpMessageHandler<TimingHandler>();
                                //services.AddOptions<HostOptions>().Configure(o => o.ShutdownTimeout = TimeSpan.FromSeconds(10));
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
                            //.UseSystemd();

            try
            {
                //var build = builder.Build();
                //using (var cancellationTokenSource = new CancellationTokenSource())
                //{
                //    Console.CancelKeyPress += (sender, e) =>
                //    {
                //        //ColorConsole.Write("\n> ".Green(), "Quit? ", " (Y/N) ".Green());
                //        //var quit = Console.ReadKey();
                //        //if (quit.Key != ConsoleKey.Y)
                //        //{
                //        e.Cancel = true;
                //        cancellationTokenSource.Cancel();
                //        //}
                //    };

                //    await build.StartAsync(cancellationTokenSource.Token);
                //}
                await builder.RunConsoleAsync(options => options.SuppressStatusMessages = true);
            }
            catch (Exception ex)
            {
                ColorConsole.WriteLine(ex.Message.White().OnRed());
            }
        }
    }
}
