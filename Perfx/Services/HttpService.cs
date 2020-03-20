namespace Perfx
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Newtonsoft.Json;

    public class HttpService : IDisposable
    {
        public const string RequestId = "Request-Id";
        private const string OperationId = "operation_Id";
        private const string AuthHeader = "Authorization";
        private const string Bearer = "Bearer ";

        private readonly JsonSerializer jsonSerializer;
        private readonly ILogger<HttpService> logger;
        private readonly HttpClient client;

        private bool disposedValue = false;

        private Settings settings;

        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        public HttpService(IHttpClientFactory httpClientFactory, IOptionsMonitor<Settings> settingsMonitor, JsonSerializer jsonSerializer, ILogger<HttpService> logger)
        {
            this.client = httpClientFactory.CreateClient(nameof(Perfx));
            this.client.Timeout = Timeout.InfiniteTimeSpan;
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.jsonSerializer = jsonSerializer;
            this.logger = logger;
        }

        public async Task<Result> ProcessRequest(Result record, CancellationToken stopToken = default)
        {
            var token = this.settings.Token;
            this.client.DefaultRequestHeaders.Clear();
            this.client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
            this.client.DefaultRequestHeaders.Add(RequestId, record.op_Id);
            record.timestamp = DateTime.Now;
            var input = record.input;
            var taskWatch = Stopwatch.StartNew();
            try
            {
                // See: https://docs.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps
                // Credit: https://josefottosson.se/you-are-probably-still-using-httpclient-wrong-and-it-is-destabilizing-your-software/
                var response = await this.client.SendAsync(new HttpRequestMessage(new HttpMethod(input.Method), record.url), this.settings.ReadResponseHeadersOnly ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, stopToken);
                record.local_ms = taskWatch.ElapsedMilliseconds;
                record.result = $"{(int)response.StatusCode}: {response.ReasonPhrase}";
                record.size_b = response.Content.Headers.ContentLength;
                //using (var responseStream = await response.Content.ReadAsStreamAsync())
                //{
                //    using (var streamReader = new StreamReader(responseStream))
                //    using (var jsonTextReader = new JsonTextReader(streamReader))
                //    {
                //        if (!response.IsSuccessStatusCode)
                //        {
                //            var err = jsonSerializer.Deserialize<InvalidAuthTokenError>(jsonTextReader);
                //            if (err == null)
                //            {
                //                ColorConsole.WriteLine($"GetJson - {response.StatusCode}: {response.ReasonPhrase}\n{await jsonTextReader.ReadAsStringAsync()}");
                //            }
                //            else
                //            {
                //                ColorConsole.WriteLine($"GetJson - {response.StatusCode}: {response.ReasonPhrase}\n{err.error.code}: {err.error.message}".White().OnRed());
                //            }
                //        }
                //        else
                //        {
                //            return (jsonSerializer.Deserialize<T>(jsonTextReader), elapsedTime);
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                record.local_ms = taskWatch.ElapsedMilliseconds;
                record.result = ex.Message;
                this.logger.LogTrace($"ERR: {ex.Message}: {record.id} - {record.url} ({record.op_Id})");
            }

            return record;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.client.Dispose();
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
