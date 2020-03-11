namespace Perfx
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Running;

    using ColoredConsole;

    using Newtonsoft.Json;

    public class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            // Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintHelp();
            var tenant = string.Empty;
            do
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
                    else if (key.Equals("r", StringComparison.OrdinalIgnoreCase) || key.Equals("a", StringComparison.OrdinalIgnoreCase))
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

                        // input.Token = await AuthHelper.GetAuthToken(tenant);
                        authInfo.Token = await AuthHelper.GetAuthTokenSilentAsync(authInfo);
                        File.WriteAllText(Utils.AuthInfoFile, JsonConvert.SerializeObject(authInfo, Formatting.Indented));

                        // https://benchmarkdotnet.org/articles/configs/configoptions.html
                        var config = ManualConfig.Create(DefaultConfig.Instance).With(ConfigOptions.DisableOptimizationsValidator);
                        var summary = BenchmarkRunner.Run<PerfRunner>(config);
                        ColorConsole.WriteLine(summary.ToString());
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
            while (true);
        }

        private static void PrintHelp()
        {
            ColorConsole.WriteLine(
                new[]
                {
                    "--------------------------------------------------------------".Green(),
                    "\nEnter ", "r".Green(), " to run the performance benchmarks",
                    "\nEnter ", "c".Green(), " to clear the console",
                    "\nEnter ", "q".Green(), " to quit",
                    "\nEnter ", "?".Green(), " to print this help"
                });
        }
    }
}
