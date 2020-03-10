namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Running;

    using ColoredConsole;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class Program
    {
        // Credit: https://stackoverflow.com/questions/26384034/how-to-get-the-azure-account-tenant-id
        private const string TenantInfoUrl = "https://login.windows.net/{0}.onmicrosoft.com/.well-known/openid-configuration";
        private static HttpClient Client = new HttpClient();

        // e.g.: "https://management.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources?api-version=2017-05-10"
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
                        var input = new Input();
                        if (!File.Exists(Utils.InputFile))
                        {
                            foreach (var prop in input.Properties.Where(p => p.Name != nameof(input.Token)))
                            {
                                var value = prop.GetValue(input) ?? string.Empty;
                                ColorConsole.Write($"{prop.Name}".Green(), ": ");
                                var val = Console.ReadLine();
                                if (!string.IsNullOrEmpty(val) && value.ToString() != val.Trim())
                                {
                                    var isCollection = prop.PropertyType.Namespace.Equals("System.Collections.Generic");
                                    prop.SetValue(input, isCollection ? val.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()) : Convert.ChangeType(val, prop.PropertyType));
                                }
                            }
                        }
                        else
                        {
                            input = JsonConvert.DeserializeObject<Input>(File.ReadAllText(Utils.InputFile));
                            if (input.Endpoints?.Count() <= 0)
                            {
                                ColorConsole.WriteLine("> ".Green(), $"Enter the Urls to benchmark (comma-separated): ");
                                var urls = Console.ReadLine();
                                input.Endpoints = urls.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                            }
                        }

                        // input.Token = await AuthHelper.GetAuthToken(tenant);
                        input.Token = await AuthHelper.GetAuthTokenSilentAsync(input);
                        File.WriteAllText(Utils.InputFile, JsonConvert.SerializeObject(input, Formatting.Indented));

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
