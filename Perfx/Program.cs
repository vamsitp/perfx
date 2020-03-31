namespace Perfx
{
    using System;
    using System.IO;
    using System.Linq;
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
            //if (Console.WindowWidth < Console.LargestWindowWidth - 10 || Console.WindowHeight < Console.LargestWindowHeight - 5)
            //{
            //    Console.WriteLine($"Resizing: w - {Console.WindowWidth} -> {Console.LargestWindowWidth - 10} / h - {Console.WindowHeight} -> {Console.LargestWindowHeight - 5}");
            //    Console.SetWindowPosition(0, 0);
            //    Console.SetWindowSize(Console.LargestWindowWidth - 10, Console.LargestWindowHeight - 5);
            //}
            Console.OutputEncoding = Encoding.UTF8;

            // Credit: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-3.1
            // Credit: https://thecodebuzz.com/using-httpclientfactory-in-net-core-console-application/
            IConfiguration configuration = null;
            var builder = Host
                            .CreateDefaultBuilder(args)
                            //.ConfigureHostConfiguration(configHost => { })
                            .ConfigureAppConfiguration((hostContext, config) =>
                            {
                                config
                                    .SetBasePath(string.Empty.GetFullPath())
                                    .AddJsonFile(GetAppSettingsFile(args), optional: true, reloadOnChange: true)
                                    .AddJsonFile(Settings.DefaultLogSettingsFile, optional: true, reloadOnChange: true);
                                configuration = config.Build();
                            })
                            .ConfigureServices((hostContext, services) =>
                            {
                                services
                                    .Configure<Settings>(configuration)
                                    .PostConfigure<Settings>(config => { config.AppSettingsFile = GetAppSettingsFile(args); })
                                    .AddScoped<BenchmarkService>()
                                    .AddScoped<HttpService>()
                                    .AddHostedService<Worker>()
                                    .AddSingleton<LogDataService>()
                                    .AddTransient<TimingHandler>()
                                    .AddSingleton<JsonSerializer>()
                                    .AddHttpClient(nameof(Perfx))
                                    .AddHttpMessageHandler<TimingHandler>();

                                var plugin = PluginLoader.LoadPlugin(configuration.Get<Settings>());
                                if (plugin != null)
                                {
                                    services.AddSingleton<IPlugin>(plugin);
                                }

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

        private static string GetAppSettingsFile(string[] args)
        {
            if (args?.Length > 0)
            {
                var file = args.FirstOrDefault().GetFullPathEx();
                if (File.Exists(file))
                {
                    return file;
                }

                ColorConsole.WriteLine("Unable to load ".DarkGray(), file.Gray());
            }

            return Settings.DefaultAppSettingsFile;
        }
    }
}
