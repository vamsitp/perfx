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
            try
            {
                // Credit: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-3.1
                // Credit: https://thecodebuzz.com/using-httpclientfactory-in-net-core-console-application/
                IConfiguration configuration = null;
                var appSettingsFile = GetAppSettingsFile(args);
                appSettingsFile.SetBasePath();
                var builder = Host
                .CreateDefaultBuilder(args)
                //.ConfigureHostConfiguration(configHost => { })
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config
                        .SetBasePath(string.Empty.GetFullPath())
                        .AddJsonFile(appSettingsFile, optional: true, reloadOnChange: true)
                        .AddJsonFile(Settings.DefaultLogSettingsFile, optional: true, reloadOnChange: true);
                    configuration = config.Build();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .Configure<Settings>(configuration)
                        .PostConfigure<Settings>(config => { config.AppSettingsFile = appSettingsFile; })
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
                else
                {
                    file = args.FirstOrDefault().GetFullPathEx("Settings.json");
                    if (File.Exists(file))
                    {
                        return file;
                    }
                }

                ColorConsole.WriteLine("Searched for ", file.DarkGray(), " or ", file.Replace(".Settings", string.Empty).DarkGray());
            }

            var settingsFile = Directory.EnumerateFiles(string.Empty.GetFullPath(), "*.Settings.json").Where(f => (args?.FirstOrDefault() == null ? f.Contains(Path.GetFileName(Settings.DefaultAppSettingsFile), StringComparison.OrdinalIgnoreCase) : f.Contains(args.FirstOrDefault(), StringComparison.OrdinalIgnoreCase)) && !f.Contains("_Results.json", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            return settingsFile ?? Settings.DefaultAppSettingsFile;
        }
    }
}
