﻿namespace Perfx
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
    using Microsoft.Identity.Client;
    using Microsoft.Identity.Client.Extensions.Msal;

    public class Worker : BackgroundService
    {
        private Settings settings;
        private IEnumerable<IOutput> outputs;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly ILogger<Worker> logger;
        private readonly IHostApplicationLifetime appLifetime;
        private readonly IEnumerable<IOutput> allOutputs;
        private readonly IPlugin plugin;
        private readonly LogDataService logDataService;

        public Worker(IServiceScopeFactory serviceScopeFactory, IOptionsMonitor<Settings> settingsMonitor, LogDataService logDataService, ILogger<Worker> logger, IServiceProvider services, IHostApplicationLifetime appLifetime, IEnumerable<IOutput> allOutputs)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => { this.settings = changedSettings; this.outputs = null; });
            this.serviceScopeFactory = serviceScopeFactory;
            this.logger = logger;
            this.appLifetime = appLifetime;
            this.allOutputs = allOutputs;
            this.plugin = services.GetService<IPlugin>();
            this.logDataService = logDataService;
        }

        private IEnumerable<IOutput> Outputs
        {
            get
            {
                if (outputs == null)
                {
                    outputs = this.allOutputs.Where(o => settings.Outputs.Any(x => o.GetType().Name.StartsWith(x.Format.ToString())));
                    if (!(outputs?.Any() == true))
                    {
                        outputs = new[] { this.plugin };
                    }
                }

                return outputs;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            var tenant = string.Empty;
            ColorConsole.WriteLine("https://vamsitp.github.io/perfx".Green(),
                "\n--------------------------------------------------------------".Green(),
                "\nLoaded".Gray(), ": ".Green(), this.settings.AppSettingsFile.DarkGray(), " (", PathExtensions.BasePath.DarkGray() ,")");
            if (!this.settings.QuiteMode)
            {
                PrintHelp();
            }
            else
            {
                ColorConsole.WriteLine("Running in ", nameof(this.settings.QuiteMode).DarkGray(), " ...");
            }

            IList<Result> results = null;
            if (!Directory.Exists(string.Empty.GetFullPath()))
            {
                Directory.CreateDirectory(string.Empty.GetFullPath());
            }

            do
            {
                try
                {
                    ColorConsole.Write("\n> ".Green());
                    var key = this.settings.QuiteMode ? "r" : Console.ReadLine()?.Trim();
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
                        if (results == null)
                        {
                            results = await this.Outputs.FirstOrDefault().Read<Result>(this.settings);
                        }

                        if (results?.Count > 0)
                        {
                            using (var scope = serviceScopeFactory.CreateScope())
                            {
                                var benchmark = scope.ServiceProvider.GetRequiredService<BenchmarkService>();
                                await ExecuteAppInsights(results, key, stopToken);
                                await this.Outputs.Save(results, this.settings);
                                results.DrawStats();
                            }
                        }
                    }
                    else if (key.StartsWith("s", StringComparison.OrdinalIgnoreCase) || key.StartsWith("d", StringComparison.OrdinalIgnoreCase) || key.StartsWith("b", StringComparison.OrdinalIgnoreCase))
                    {
                        if (results == null)
                        {
                            results = await this.Outputs.FirstOrDefault().Read<Result>(this.settings);
                        }

                        if (results?.Count > 0)
                        {
                            // results.Save(this.settings.OutputFormat);
                            results.DrawStats();
                        }
                    }
                    else if (key.StartsWith("r", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!this.settings.QuiteMode && !File.Exists(settings.AppSettingsFile))
                        {
                            foreach (var prop in settings.Properties)
                            {
                                var value = prop.GetValue(settings) ?? string.Empty;
                                ColorConsole.Write($"{prop.Name}".Green(), $" ({prop.PropertyType}): ");
                                var val = Console.ReadLine();
                                if (!string.IsNullOrEmpty(val) && value.ToString() != val.Trim())
                                {
                                    var isCollection = prop.PropertyType.Namespace.Equals("System.Collections.Generic");
                                    prop.SetValue(settings, isCollection ? val.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()) : Convert.ChangeType(val, prop.PropertyType));
                                }
                            }

                            settings.Save();
                        }

                        if (!this.settings.QuiteMode && (settings.Endpoints == null || (settings.Endpoints != null && settings.Endpoints.Count() == 0)))
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

                        try
                        {
                            if (this.plugin != null)
                            {
                                settings.Token = await this.plugin.GetAuthToken(settings);
                            }
                            else
                            {
                                ////// TODO
                                //// if (!Guid.TryParse(settings.Tenant, out var tenantId))
                                //// {
                                ////     settings.Tenant = await AuthHelper.GetTenantId(settings.Tenant);
                                //// }
                                await SetAuthToken();
                            }
                        }
                        catch (Exception ex) when (ex is NotImplementedException || ex is NotSupportedException)
                        {
                            await SetAuthToken();
                        }

                        using (var scope = serviceScopeFactory.CreateScope())
                        {
                            var benchmark = scope.ServiceProvider.GetRequiredService<BenchmarkService>();
                            var split = key.Split(new[] { ':', '=', '-', '/' }, 2);
                            int? iterations = split.Length > 1 && int.TryParse(split[1]?.Trim(), out var r) ? r : default(int?);
                            results = await benchmark.Execute(iterations, stopToken);
                            ColorConsole.Write("> ".Green(), "Fetch", $" [{results.Count}]".Green(), " durations", " from App-Insights?", " (Y/N) ".Green());
                            var result = this.settings.QuiteMode ? "y" : Console.ReadLine();
                            if (result?.StartsWith("y", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                ColorConsole.WriteLine();
                                await ExecuteAppInsights(results, result, stopToken);
                            }

                            await this.Outputs.Save(results, this.settings);
                            results.DrawStats();
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
            while (!stopToken.IsCancellationRequested && !this.settings.QuiteMode);

            // If 90% of requests are > the expected response-time-sla or max-response-size > response-size-sla or error-status-code > 1%
            var warnings = results?.GetStats().Count(s => s.dur_90_perc_s > s.sla_dur_s || s.size_max_kb > s.sla_size_kb || s.other_xxx > 1) ?? 0;
            ColorConsole.WriteLine("dur_90_perc_s > sla_dur_s ", "(OR)".White(), " size_max_kb > sla_size_kb ", "(OR)".White(), " other_xxx > 1% ".DarkGray(), ": ".Green(), warnings.ToString().Yellow());
            Environment.ExitCode = warnings; // Check %errorlevel% after the app exists
            this.appLifetime.StopApplication();
        }

        private async Task<AuthenticationResult> SetAuthToken()
        {
            ColorConsole.Write("authenticating".DarkGray(), "... ");
            // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Integrated-Windows-Authentication
            // https://learn.microsoft.com/en-in/azure/active-directory/develop/scenario-desktop-acquire-token-interactive?tabs=dotnet
            var authority = this.settings.Authority;
            var scopes = new string[] { "user.read" };
            var app = PublicClientApplicationBuilder
                 .Create(this.settings.ClientId)
                 .WithAuthority(authority)
                 .WithRedirectUri("http://localhost")
                 .Build();

            // https://github.com/Azure-Samples/ms-identity-dotnet-desktop-tutorial/blob/master/2-TokenCache/Console-TokenCache/Program.cs
            var storageProperties =
                 new StorageCreationPropertiesBuilder(CacheSettings.CacheFileName, CacheSettings.CacheDir)
                 .WithCacheChangedEvent(this.settings.ClientId, authority)
                 .WithLinuxKeyring(
                     CacheSettings.LinuxKeyRingSchema,
                     CacheSettings.LinuxKeyRingCollection,
                     CacheSettings.LinuxKeyRingLabel,
                     CacheSettings.LinuxKeyRingAttr1,
                     CacheSettings.LinuxKeyRingAttr2)
                 .WithMacKeyChain(CacheSettings.KeyChainServiceName, CacheSettings.KeyChainAccountName)
                 .Build();

            // This hooks up the cross-platform cache into MSAL
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var accounts = await app.GetAccountsAsync();
            AuthenticationResult result = null!;
            var needsLogin = false;
            if (accounts.Any())
            {
                try
                {
                    result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                }
                catch (MsalUiRequiredException) // https://learn.microsoft.com/en-us/azure/active-directory/develop/msal-error-handling-dotnet#msaluirequiredexception
                {
                    needsLogin = true;
                }
            }
            else
            {
                needsLogin = true;
            }

            if (needsLogin)
            {
                try
                {
                    result = await app.AcquireTokenInteractive(scopes).ExecuteAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.White().OnRed());
                }
            }

            settings.Token = result?.AccessToken;
            return result;
        }

        private async Task ExecuteAppInsights(IList<Result> results, string key, CancellationToken stopToken)
        {
            var split = key.Split(new[] { ':', '=', '-', '/' }, 3);
            var timeframe = split.Length > 1 ? split[1]?.Trim() : "60m";
            var retries = split.Length > 2 && int.TryParse(split[2]?.Trim(), out var r) ? r : 60;
            await this.logDataService.ExecuteAppInsights(results, timeframe, retries, stopToken);
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

    public static class CacheSettings
    {
        // computing the root directory is not very simple on Linux and Mac, so a helper is provided
        private static readonly string s_cacheFilePath = Path.Combine(MsalCacheHelper.UserRootDirectory, "msal.perfx.cache");

        public static readonly string CacheFileName = Path.GetFileName(s_cacheFilePath);
        public static readonly string CacheDir = Path.GetDirectoryName(s_cacheFilePath);


        public static readonly string KeyChainServiceName = "Perfx";
        public static readonly string KeyChainAccountName = "MSALCache";

        public static readonly string LinuxKeyRingSchema = "com.perfx.tokencache";
        public static readonly string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
        public static readonly string LinuxKeyRingLabel = "MSAL token cache for Perfx";
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1");
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("ProductGroup", "Perfx");
    }
}
