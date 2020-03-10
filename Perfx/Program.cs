namespace Perfx
{
    using System;
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
                        ColorConsole.Write("> ".Green(), $"Azure (AD) Tenant/Directory name to login (e.g. ", "abc".Green(), $" in 'abc.onmicrosoft.com'): ");
                        tenant = Console.ReadLine();
                        if (!Guid.TryParse(tenant, out var tenantId))
                        {
                            var tenantName = tenant.ToLowerInvariant().Replace(".onmicrosoft.com", string.Empty);
                            var response = await Client.GetAsync(string.Format(TenantInfoUrl, tenantName));
                            var result = await response.Content.ReadAsStringAsync();
                            var json = JsonConvert.DeserializeObject<JObject>(result);
                            tenant = json?.SelectToken(".issuer")?.Value<string>()?.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault();
                            ColorConsole.WriteLine("ID: ", tenant.Green());
                        }

                        var token = await AuthHelper.GetAuthToken(tenant);
                        ColorConsole.WriteLine("> ".Green(), $"Enter the Urls to benchmark (comma-separated): ");
                        var urls = Console.ReadLine();
                        var input = new Input { Token = token, Endpoints = urls.Split(',', StringSplitOptions.RemoveEmptyEntries) };
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
