namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class Worker : BackgroundService
    {
        private Settings settings;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly ILogger<Worker> logger;
        private readonly IServiceProvider services;
        private readonly IPlugin plugin;

        public Worker(IServiceScopeFactory serviceScopeFactory, IOptionsMonitor<Settings> settingsMonitor, ILogger<Worker> logger, IPlugin plugin, IServiceProvider services)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.serviceScopeFactory = serviceScopeFactory;
            this.logger = logger;
            this.services = services;
            this.plugin = plugin;
        }

        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            var tenant = string.Empty;
            PrintHelp();
            List<Record> records = null;
            if (!Directory.Exists(string.Empty.GetFullPath()))
            {
                Directory.CreateDirectory(string.Empty.GetFullPath());
            }

            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    ColorConsole.Write("\n> ".Green());
                    var key = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        PrintHelp();
                        continue;
                    }
                    else if (key.StartsWith("q", StringComparison.OrdinalIgnoreCase) || key.StartsWith("exit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("close", StringComparison.OrdinalIgnoreCase))
                    {
                        // ColorConsole.WriteLine(" Quiting... ".White().OnDarkGreen());
                        break;
                    }
                    else if (key.Equals("?") || key.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                    {
                        PrintHelp();
                    }
                    else if (key.StartsWith("c", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Clear();
                    }
                    else if (key.StartsWith("t", StringComparison.OrdinalIgnoreCase))
                    {
                        this.logger.ShowThreads(true);
                    }
                    else if (key.StartsWith("l", StringComparison.OrdinalIgnoreCase) || key.StartsWith("a", StringComparison.OrdinalIgnoreCase))
                    {
                        if (records == null)
                        {
                            records = this.settings.OutputFormat.Read<Record>();
                        }

                        if (records?.Count > 0)
                        {
                            using (var scope = serviceScopeFactory.CreateScope())
                            {
                                var perf = scope.ServiceProvider.GetRequiredService<PerfRunner>();
                                await ExecuteAppInsights(records, key, perf, stopToken);
                                records.Save(this.settings.OutputFormat);
                                records.DrawStats();
                            }
                        }
                    }
                    else if (key.StartsWith("s", StringComparison.OrdinalIgnoreCase) || key.StartsWith("d", StringComparison.OrdinalIgnoreCase) || key.StartsWith("b", StringComparison.OrdinalIgnoreCase))
                    {
                        if (records == null)
                        {
                            records = this.settings.OutputFormat.Read<Record>();
                        }

                        if (records?.Count > 0)
                        {
                            // records.Save(this.settings.OutputFormat);
                            records.DrawStats();
                        }
                    }
                    else if (key.StartsWith("r", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!File.Exists(Settings.AppSettingsFile))
                        {
                            foreach (var prop in settings.Properties.Where(p => p.Name != nameof(settings.Properties) && p.Name != nameof(settings.Token)))
                            {
                                var value = prop.GetValue(settings) ?? string.Empty;
                                ColorConsole.Write($"{prop.Name}".Green(), $"({prop.PropertyType}): ");
                                var val = Console.ReadLine();
                                if (!string.IsNullOrEmpty(val) && value.ToString() != val.Trim())
                                {
                                    var isCollection = prop.PropertyType.Namespace.Equals("System.Collections.Generic");
                                    prop.SetValue(settings, isCollection ? val.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()) : Convert.ChangeType(val, prop.PropertyType));
                                }
                            }

                            settings.Save();
                        }

                        if (settings.Endpoints == null || (settings.Endpoints != null && settings.Endpoints.Count() == 0))
                        {
                            ColorConsole.WriteLine("\n> ".Green(), "Enter the Urls to benchmark (comma-separated): ");
                            var urls = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(urls))
                            {
                                settings.Endpoints = urls.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                                settings.Save();
                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(settings.UserId) && !string.IsNullOrEmpty(settings.Password))
                        {
                            try
                            {
                                settings.Token = await this.plugin.GetAuthToken(settings);
                            }
                            catch (Exception ex) when (ex is NotImplementedException || ex is NotSupportedException)
                            {
                                var defaultPlugin = this.services.GetServices<IPlugin>().SingleOrDefault(p => p is PluginService);
                                settings.Token = await defaultPlugin.GetAuthToken(settings);
                            }
                        }

                        using (var scope = serviceScopeFactory.CreateScope())
                        {
                            var perf = scope.ServiceProvider.GetRequiredService<PerfRunner>();
                            var split = key.Split(new[] { ':', '=', '-', '/' }, 2);
                            int? iterations = split.Length > 1 && int.TryParse(split[1], out var r) ? r : default(int?);
                            records = await perf.Execute(iterations, stopToken);
                            ColorConsole.Write("> ".Green(), "Fetch ", $" [{records.Count}]".Green(), " durations", " from App-Insights?", " (Y/N) ".Green());
                            var result = Console.ReadLine();
                            if (result.StartsWith("y", StringComparison.OrdinalIgnoreCase))
                            {
                                ColorConsole.WriteLine();
                                await ExecuteAppInsights(records, result, perf, stopToken);
                            }

                            records.Save(this.settings.OutputFormat);
                            records.DrawStats();
                        }
                    }
                    else // (string.IsNullOrWhiteSpace(key))
                    {
                        PrintHelp();
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.White().OnRed());
                }
            }

            // Clean-up on cancellation
        }

        private static async Task ExecuteAppInsights(List<Record> records, string key, PerfRunner perf, CancellationToken stopToken)
        {
            var split = key.Split(new[] { ':', '=', '-', '/' }, 3);
            var timeframe = split.Length > 1 ? split[1] : "60m";
            var retries = split.Length > 2 && int.TryParse(split[2], out var r) ? r : 60;
            await perf.ExecuteAppInsights(records, timeframe, retries, stopToken);
        }

        private static void PrintHelp()
        {
            ColorConsole.WriteLine(
                new[]
                {
                    "--------------------------------------------------------------".Green(),
                    "\nEnter ", "r".Green(), ":10".DarkGray(), " to run the benchmarks", " 10 times".DarkGray(),
                    "\nEnter ", "s".Green(), " to print the stats/details for the previous run",
                    "\nEnter ", "l".Green(), ":1h".DarkGray(), ":10".DarkGray(), " to fetch app-insights duration logs for the previous run (in the last", " 1 hour".DarkGray(), " with", " 10 retries".DarkGray(), ")",
                    "\nEnter ", "c".Green(), " to clear the console",
                    "\nEnter ", "q".Green(), " to quit",
                    "\nEnter ", "?".Green(), " to print this help"
                });
        }
    }
}
