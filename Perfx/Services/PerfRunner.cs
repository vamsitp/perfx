namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BenchmarkDotNet.Attributes;

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

        private Input input;

        [GlobalSetup]
        public void Setup()
        {
            this.input = JsonConvert.DeserializeObject<Input>(File.ReadAllText(Utils.InputFile));
        }

        [Benchmark]
        [ArgumentsSource(nameof(Endpoints))]
        public async Task Execute(string endpoint)
        {
            var traceId = Guid.NewGuid().ToString();
            var response = await GetJson<dynamic>(endpoint, traceId);
            string result = JsonConvert.SerializeObject(response);
            ColorConsole.WriteLine("\nResponse received for ", traceId.Green(), $": {result.Substring(0, result.Length > MaxLength ? MaxLength : result.Length)}", " ...".Green());
        }

        private async Task<T> GetJson<T>(string endpoint, string traceId)
        {
            var token = this.input.Token;
            this.Client.DefaultRequestHeaders.Remove(AuthHeader);
            this.Client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
            this.Client.DefaultRequestHeaders.Add(RequestId, traceId);
            this.Client.DefaultRequestHeaders.Add(OperationId, traceId);
            var response = await this.Client.GetAsync(endpoint);
            var result = await response.Content.ReadAsStringAsync();
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

            return JsonConvert.DeserializeObject<T>(result);
        }

        public IEnumerable<object> Endpoints()
        {
            if (this.input == null)
            {
                this.input = JsonConvert.DeserializeObject<Input>(File.ReadAllText(Utils.InputFile));
            }

            return this.input.Endpoints;
        }
    }
}
