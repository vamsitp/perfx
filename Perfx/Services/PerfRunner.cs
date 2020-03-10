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
        private const string Bearer = "Bearer ";

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
            var response = await GetJson<dynamic>(endpoint);
            ColorConsole.WriteLine(response);
        }

        private async Task<T> GetJson<T>(string path)
        {
            var token = this.input.Token;
            this.Client.DefaultRequestHeaders.Remove(AuthHeader);
            this.Client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
            var response = await this.Client.GetAsync(path);
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

    public class Input
    {
        public string Token { get; set; }
        public IEnumerable<string> Endpoints { get; set; }
    }

    public class InvalidAuthTokenError
    {
        public Error error { get; set; }
    }

    public class Error
    {
        public string code { get; set; }
        public string message { get; set; }
    }
}
