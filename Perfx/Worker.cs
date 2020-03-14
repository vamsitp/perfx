﻿namespace Perfx
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;

    public class Worker : BackgroundService
    {
        private Settings settings;
        private readonly IServiceScopeFactory serviceScopeFactory;

        public Worker(IServiceScopeFactory serviceScopeFactory, IOptionsMonitor<Settings> settingsMonitor)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            var tenant = string.Empty;
            PrintHelp();
            var results = Utils.ReadResults<dynamic>()?.Select(x =>
            {
                (string traceId, double duration) r = (x.traceId, double.Parse(x.duration));
                return r;
            })?.ToList();
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
                        ColorConsole.WriteLine("DONE!".White().OnDarkGreen());
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
                    else if (key.StartsWith("l", StringComparison.OrdinalIgnoreCase) || key.StartsWith("a", StringComparison.OrdinalIgnoreCase))
                    {
                        if (results?.Count > 0)
                        {
                            using (var scope = serviceScopeFactory.CreateScope())
                            {
                                var perf = scope.ServiceProvider.GetRequiredService<PerfRunner>();
                                await perf.LogAppInsights(results);
                            }
                        }
                    }
                    else if (key.StartsWith("d", StringComparison.OrdinalIgnoreCase) || key.StartsWith("b", StringComparison.OrdinalIgnoreCase))
                    {
                        if (results?.Count > 0)
                        {
                            results.DrawChart();
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
                            // input.Token = await AuthHelper.GetAuthToken(tenant);
                            settings.Token = await AuthHelper.GetAuthTokenSilentAsync(settings);
                        }

                        using (var scope = serviceScopeFactory.CreateScope())
                        {
                            var perf = scope.ServiceProvider.GetRequiredService<PerfRunner>();
                            results = await perf.Execute();
                            ColorConsole.Write("\n> ".Green(), "Fetch ", "durations".Green(), " from App-Insights?", " (Y/N) ".Green());
                            var result = Console.ReadKey();
                            ColorConsole.WriteLine();
                            if (result.Key == ConsoleKey.Y)
                            {
                                ColorConsole.WriteLine();
                                await perf.LogAppInsights(results);
                            }

                            results.DrawChart();
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

        private static void PrintHelp()
        {
            ColorConsole.WriteLine(
                new[]
                {
                    "--------------------------------------------------------------".Green(),
                    "\nEnter ", "r".Green(), " to run the benchmarks",
                    "\nEnter ", "l".Green(), " to fetch logs for the previous run (app-insights durations)",
                    "\nEnter ", "c".Green(), " to clear the console",
                    "\nEnter ", "q".Green(), " to quit",
                    "\nEnter ", "?".Green(), " to print this help"
                });
        }
    }
}