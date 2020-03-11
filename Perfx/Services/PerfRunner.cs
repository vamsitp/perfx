namespace Perfx
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Newtonsoft.Json;

    // https://benchmarkdotnet.org/articles/overview.html
    public class PerfRunner
    {
        private const string AuthHeader = "Authorization";
        private const string RequestId = "Request-Id";
        private const string OperationId = "operation_Id";
        private const string Bearer = "Bearer ";
        private const int MaxLength = 50;

        private HttpClient Client = new HttpClient();

        private AuthInfo authInfo;

        public async Task Execute(AuthInfo authInfo)
        {
            this.authInfo = authInfo;
            var endpoints = authInfo.Endpoints.Select(ep => Execute(ep, authInfo.Iterations));
            var tasks = endpoints.ToArray();
            var results = await Task.WhenAll(tasks);

            ColorConsole.WriteLine("\n", " RESULTS ".White().OnGreen());
            foreach (var r in results)
            {
                foreach (var result in r)
                {
                    ColorConsole.WriteLine($"{result.index + 1}. ".Green(), result.endpoint, " (", result.traceId.Green(), ")", ": ".Green(), result.duration.ToString("F2", CultureInfo.InvariantCulture), " ms".Green(), " (", (result.duration / 1000.00).ToString("F2", CultureInfo.InvariantCulture), " s".Green(), ")");
                }
            }
        }

        private async Task<(int index, string endpoint, string traceId, double duration)[]> Execute(string endpoint, int interations)
        {
            var result = await Task.WhenAll(Enumerable.Range(0, interations).Select(async i =>
            {
                var traceId = Guid.NewGuid().ToString();
                var taskWatch = new Stopwatch();
                taskWatch.Start();
                var response = await GetJson<dynamic>(endpoint, traceId);
                taskWatch.Stop();
                string result = JsonConvert.SerializeObject(response);
                ColorConsole.WriteLine($"{i + 1}. ".Green(), endpoint, " (", traceId.Green(), ")", ":".Green(), $" {result.Substring(0, result.Length > MaxLength ? MaxLength : result.Length)}", " ...".Green());
                return (i, endpoint, traceId, taskWatch.Elapsed.TotalMilliseconds);
            }));

            return result;
        }

        private async Task<T> GetJson<T>(string endpoint, string traceId)
        {
            var result = string.Empty;
            try
            {
                var token = this.authInfo.Token;
                this.Client.DefaultRequestHeaders.Clear();
                this.Client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
                this.Client.DefaultRequestHeaders.Add(RequestId, traceId);
                this.Client.DefaultRequestHeaders.Add(OperationId, traceId);
                var response = await this.Client.GetAsync(endpoint);
                result = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    var err = JsonConvert.DeserializeObject<InvalidAuthTokenError>(result);
                    if (err == null)
                    {
                        ColorConsole.WriteLine($"GetJson - {response.StatusCode}: {response.ReasonPhrase}\n{result}");
                    }
                    else
                    {
                        ColorConsole.WriteLine($"GetJson - {response.StatusCode}: {response.ReasonPhrase}\n{err.error.code}: {err.error.message}".White().OnRed());
                    }
                }
            }
            catch (Exception ex)
            {
                ColorConsole.WriteLine(ex.Message.White().OnRed());
            }

            return JsonConvert.DeserializeObject<T>(result);
        }
    }
}
