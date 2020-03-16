namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.Extensions.Options;

    using Newtonsoft.Json;

    public class PerfRunner : IDisposable
    {
        public const string RequestId = "Request-Id";
        private const string OperationId = "operation_Id";
        private const string AuthHeader = "Authorization";
        private const string Bearer = "Bearer ";

        private readonly JsonSerializer jsonSerializer;
        private readonly LogDataService logDataService;
        private bool disposedValue = false;

        private readonly HttpClient client;

        private Settings settings;

        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        public PerfRunner(IHttpClientFactory httpClientFactory, IOptionsMonitor<Settings> settingsMonitor, LogDataService logDataService, JsonSerializer jsonSerializer)
        {
            client = httpClientFactory.CreateClient(nameof(Perfx));
            client.Timeout = Timeout.InfiniteTimeSpan;
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.logDataService = logDataService;
            this.jsonSerializer = jsonSerializer;
        }

        public async Task<List<Record>> Execute()
        {
            ColorConsole.WriteLine("Endpoints: ", settings.Endpoints.Count().ToString().Green());
            ColorConsole.WriteLine("Iterations: ", settings.Iterations.ToString().Green(), "\n");

            var endpointTasks = settings.Endpoints.Select((ep, i) => Execute(ep, i + 1, settings.Iterations));
            var results = await Task.WhenAll(endpointTasks);
            var records = results.SelectMany(e => e).ToList();
            return records;
        }

        private async Task<IEnumerable<Record>> Execute(string endpoint, float topIndex, int interations)
        {
            var result = await Task.WhenAll(Enumerable.Range(0, interations).Select(async i =>
            {
                var record = new Record {id = float.Parse($"{topIndex}.{i + 1}"), traceId = Guid.NewGuid().ToString(), url = endpoint };
                var response = await ProcessRequest(record);
                await Lock.WaitAsync();
                Log(record);
                Lock.Release();
                return record;
            }));

            return result;
        }

        private void Log(Record record)
        {
            var id = record.id.ToString();
            ColorConsole.WriteLine($"{id} ", record.url.Green(), "\n",
                "stat".PadLeft(id.Length + 5).Green(), $": {record.result}", "\n",
                "opid".PadLeft(id.Length + 5).Green(), ": ", record.traceId, "\n",
                "time".PadLeft(id.Length + 5).Green(), ": ", record.duration_ms.GetColorToken(" "), " ", record.GetDurationString(), "ms".Green(), " (~", record.GetDurationInSecString(), "s".Green(), ") ", "\n");
        }

        public async Task ExecuteAppInsights(List<Record> records, string timeframe = "60m")
        {
            if (!string.IsNullOrWhiteSpace(settings.AppInsightsAppId) && !string.IsNullOrWhiteSpace(settings.AppInsightsApiKey))
            {
                ColorConsole.Write(" App-Insights ".White().OnDarkGreen());
                var found = false;
                var i = 0;
                List<LogRecord> aiLogs = null;
                do
                {
                    i++;
                    aiLogs = (await logDataService.GetLogs(records.Select(t => t.traceId), timeframe))?.ToList();
                    found = aiLogs?.Count >= records.Count;
                    ColorConsole.Write((aiLogs?.Count > 0 ? $"{aiLogs?.Count.ToString()}" : string.Empty), ".".Green());
                    await Task.Delay(1000);
                }
                while (found == false && i < 60);

                if (aiLogs?.Count > 0)
                {
                    aiLogs.ForEach(ai =>
                    {
                        var record = records.SingleOrDefault(t => t.traceId.Equals(ai.operation_ParentId, StringComparison.OrdinalIgnoreCase));
                        record.ai_duration_ms = ai.duration;
                        record.ai_op_Id = ai.operation_Id;
                        // TODO: Rest of the props
                    });
                    ColorConsole.WriteLine();
                    records.DrawTable();
                }
                else
                {
                    ColorConsole.WriteLine("\nNo logs found!".Yellow());
                }
            }
        }

        private async Task<Record> ProcessRequest(Record record)
        {
            var token = this.settings.Token;
            this.client.DefaultRequestHeaders.Clear();
            this.client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
            this.client.DefaultRequestHeaders.Add(RequestId, record.traceId);
            var taskWatch = Stopwatch.StartNew();
            try
            {
                // See: https://docs.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps
                // Credit: https://josefottosson.se/you-are-probably-still-using-httpclient-wrong-and-it-is-destabilizing-your-software/
                var response = await this.client.SendAsync(new HttpRequestMessage(HttpMethod.Get, record.url), this.settings.ReadResponseHeadersOnly ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
                record.duration_ms = taskWatch.ElapsedMilliseconds;
                record.result = $"{(int)response.StatusCode}: {response.ReasonPhrase}";
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
                record.duration_ms = taskWatch.ElapsedMilliseconds;
                ColorConsole.WriteLine(ex.Message.White().OnRed());
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
