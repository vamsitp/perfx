namespace Perfx
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Newtonsoft.Json;

    public class WorkerService : BackgroundService
    {
        private readonly IHost host;
        private readonly IServiceScopeFactory serviceScopeFactory;

        public WorkerService(IHost host, IServiceScopeFactory serviceScopeFactory)
        {
            this.host = host;
            this.serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            var tenant = string.Empty;
            PrintHelp();
            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    ColorConsole.Write("\n> ".Green());
                    var key = Console.ReadLine()?.Trim();
                    if (key.Equals("q", StringComparison.OrdinalIgnoreCase) || key.StartsWith("quit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("exit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("close", StringComparison.OrdinalIgnoreCase))
                    {
                        ColorConsole.WriteLine("DONE!".White().OnDarkGreen());
                        break;
                    }
                    else if (key.Equals("?") || key.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                    {
                        PrintHelp();
                    }
                    else if (key.Equals("c", StringComparison.OrdinalIgnoreCase) || key.StartsWith("cls", StringComparison.OrdinalIgnoreCase) || key.StartsWith("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Clear();
                    }
                    else if (key.Equals("r", StringComparison.OrdinalIgnoreCase) || key.Equals("run", StringComparison.OrdinalIgnoreCase))
                    {
                        var authInfo = new AuthInfo();
                        if (!File.Exists(Utils.AuthInfoFile))
                        {
                            foreach (var prop in authInfo.Properties.Where(p => p.Name != nameof(authInfo.Token)))
                            {
                                var value = prop.GetValue(authInfo) ?? string.Empty;
                                ColorConsole.Write($"{prop.Name}".Green(), ": ");
                                var val = Console.ReadLine();
                                if (!string.IsNullOrEmpty(val) && value.ToString() != val.Trim())
                                {
                                    var isCollection = prop.PropertyType.Namespace.Equals("System.Collections.Generic");
                                    prop.SetValue(authInfo, isCollection ? val.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()) : Convert.ChangeType(val, prop.PropertyType));
                                }
                            }
                        }
                        else
                        {
                            authInfo = JsonConvert.DeserializeObject<AuthInfo>(File.ReadAllText(Utils.AuthInfoFile));
                            if (authInfo.Endpoints?.Count() <= 0)
                            {
                                ColorConsole.WriteLine("> ".Green(), $"Enter the Urls to benchmark (comma-separated): ");
                                var urls = Console.ReadLine();
                                authInfo.Endpoints = urls.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                            }
                        }

                        if (!string.IsNullOrEmpty(authInfo.UserId) && !string.IsNullOrEmpty(authInfo.Password))
                        {
                            // input.Token = await AuthHelper.GetAuthToken(tenant);
                            authInfo.Token = await AuthHelper.GetAuthTokenSilentAsync(authInfo);
                        }

                        File.WriteAllText(Utils.AuthInfoFile, JsonConvert.SerializeObject(authInfo, Formatting.Indented));

                        using (var scope = serviceScopeFactory.CreateScope())
                        {
                            var perf = scope.ServiceProvider.GetRequiredService<PerfRunner>();
                            await perf.Execute(authInfo);
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
                    "\nEnter ", "c".Green(), " to clear the console",
                    "\nEnter ", "q".Green(), " to quit",
                    "\nEnter ", "?".Green(), " to print this help"
                });
        }
    }
}
