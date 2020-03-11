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

    public class PerfRunner : IDisposable
    {
        private const string AuthHeader = "Authorization";
        private const string RequestId = "Request-Id";
        private const string OperationId = "operation_Id";
        private const string Bearer = "Bearer ";
        private const int MaxLength = 50;

        private bool disposedValue = false;

        private HttpClient Client = HttpClientFactory.Create();

        private AuthInfo authInfo;

        public async Task Execute(AuthInfo authInfo)
        {
            ColorConsole.WriteLine("\nauth: ", authInfo.UserId.Green());
            ColorConsole.WriteLine("endpoints: ", authInfo.Endpoints.Count().ToString().Green());
            ColorConsole.WriteLine("iterations: ", authInfo.Iterations.ToString().Green(), "\n");

            this.authInfo = authInfo;
            var endpointTasks = authInfo.Endpoints.Select((ep, i) => Execute(ep, i + 1, authInfo.Iterations));
            var results = await Task.WhenAll(endpointTasks);

            ////await foreach (var results in await tasks.WhenEach())
            //foreach (var results in await Task.WhenAll(tasks))
            //{
            //    foreach (var result in results)
            //    {
            //        var bar = string.Empty.PadLeft((int)Math.Round(result.duration / 1000, MidpointRounding.AwayFromZero), ' ');
            //        ColorConsole.WriteLine($"{result.index + 1}. ".Green(), result.endpoint, " (", result.traceId.Green(), ")", ": ".Green(), result.duration.ToString("F2", CultureInfo.InvariantCulture), " ms".Green(), " (~", (result.duration / 1000.00).ToString("F2", CultureInfo.InvariantCulture), " s".Green(), ") ", bar.OnGreen());
            //    }
            //}
        }

        private async Task<(int index, string endpoint, string traceId, double duration)[]> Execute(string endpoint, float topIndex, int interations)
        {
            var result = await Task.WhenAll(Enumerable.Range(0, interations).Select(async i =>
            {
                var traceId = Guid.NewGuid().ToString();
                var response = await GetJson<dynamic>(endpoint, traceId);
                string result = JsonConvert.SerializeObject(response.value);
                var sec = (int)Math.Round(response.duration / 1000);
                var bar = string.Empty.PadLeft(sec > 1 ? sec : 1, ' ');
                var id = $"{topIndex}.{i + 1}";
                ColorToken coloredBar = bar.OnGreen();
                if (sec <= 2)
                {
                    coloredBar = bar.OnGreen();
                }
                else if (sec > 2 && sec <= 5)
                {
                    coloredBar = bar.OnDarkYellow();
                }
                else if (sec > 5 && sec <= 8)
                {
                    coloredBar = bar.OnMagenta();
                }
                else if (sec > 8)
                {
                    coloredBar = bar.OnRed();
                }

                ColorConsole.WriteLine($"{id} ".Green(), endpoint, " (", traceId.Green(), ")", ": ".Green(), response.duration.ToString("F2", CultureInfo.InvariantCulture), "ms".Green(), " (~", (response.duration / 1000.00).ToString("F1", CultureInfo.InvariantCulture), "s".Green(), ") ", coloredBar, "\n", "resp".PadLeft(id.Length + 5).Green(), $": {result.Substring(0, result.Length > MaxLength ? MaxLength : result.Length)}", " ...".Green(), "\n");
                return (i, endpoint, traceId, response.duration);
            }));

            return result;
        }

        private async Task<(T value, double duration)> GetJson<T>(string endpoint, string traceId)
        {
            var taskWatch = new Stopwatch();
            var result = string.Empty;
            try
            {
                var token = this.authInfo.Token;
                this.Client.DefaultRequestHeaders.Clear();
                this.Client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
                this.Client.DefaultRequestHeaders.Add(RequestId, traceId);
                this.Client.DefaultRequestHeaders.Add(OperationId, traceId);
                taskWatch.Start();
                var response = await this.Client.GetAsync(endpoint);
                taskWatch.Stop();
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

            return (JsonConvert.DeserializeObject<T>(result), taskWatch.Elapsed.TotalMilliseconds);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Client.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
