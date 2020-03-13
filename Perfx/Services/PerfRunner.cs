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

    using Alba.CsConsoleFormat;

    using ColoredConsole;

    using Microsoft.Extensions.Options;

    using Newtonsoft.Json;

    public class PerfRunner : IDisposable
    {
        private const string OperationId = "operation_Id";
        private const string RequestId = "Request-Id";
        private const string AuthHeader = "Authorization";
        private const string Bearer = "Bearer ";

        private readonly JsonSerializer jsonSerializer;
        private readonly LogDataService logDataService;
        private bool disposedValue = false;

        private readonly HttpClient client;

        private readonly Settings settings;

        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        public PerfRunner(IHttpClientFactory httpClientFactory, JsonSerializer jsonSerializer, IOptions<Settings> settings, LogDataService logDataService)
        {
            client = httpClientFactory.CreateClient(nameof(Perfx));
            this.jsonSerializer = jsonSerializer;
            this.logDataService = logDataService;
            this.settings = settings.Value;
        }

        public async Task Execute()
        {
            ColorConsole.WriteLine("\nauth: ", (string.IsNullOrWhiteSpace(settings.UserId) ? "none" : settings.UserId).Green());
            ColorConsole.WriteLine("endpoints: ", settings.Endpoints.Count().ToString().Green());
            ColorConsole.WriteLine("iterations: ", settings.Iterations.ToString().Green(), "\n");

            var endpointTasks = settings.Endpoints.Select((ep, i) => Execute(ep, i + 1, settings.Iterations));
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

            var traceIds = results.SelectMany(e => e.Select(r => (r.traceId, r.duration))).ToList();
            await LogAppInsights(traceIds);
        }

        private async Task LogAppInsights(List<(string traceId, double duration)> traceIds)
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
                    aiLogs = (await logDataService.GetLogs(traceIds.Select(t => t.traceId)))?.ToList();
                    found = aiLogs?.Count >= traceIds.Count;
                    ColorConsole.Write((aiLogs?.Count > 0 ? $"{aiLogs?.Count.ToString()}" : string.Empty), ".".Green());
                    await Task.Delay(1000);
                }
                while (found == false && i < 60);

                if (aiLogs?.Count > 0)
                {
                    ColorConsole.WriteLine();

                    // Credit: https://stackoverflow.com/a/49032729
                    // https://github.com/Athari/CsConsoleFormat/blob/master/Alba.CsConsoleFormat.Tests/Elements/Containers/GridTests.cs
                    var headerThickness = new LineThickness(LineWidth.Single, LineWidth.Double);
                    var rowThickness = new LineThickness(LineWidth.Single, LineWidth.Single);
                    var doc = new Document(
                                new Grid
                                {
                                    Stroke = new LineThickness(LineWidth.None),
                                    StrokeColor = ConsoleColor.DarkGray,
                                    Columns =
                                    {
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto, MaxWidth = 200 },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto }
                                    },
                                    Children = {
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " # " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " url " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " op_Id " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " result " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " ai_duration " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.DarkGreen, Background = ConsoleColor.White, Children = { " perfx_duration " } },
                                        aiLogs.Select(log => new[]
                                        {
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Right, Children = { log.id } },
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, Children = { log.url } },
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { log.operation_ParentId } },
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { log.resultCode } },
                                            new Cell { Stroke = rowThickness, Color = log.duration.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { log.duration.ToString("F2") + "ms / " + $"{(log.duration / 1000.00).ToString("F1", CultureInfo.InvariantCulture)}s" } },
                                            new Cell { Stroke = rowThickness, Color = traceIds.SingleOrDefault(t => t.traceId.Equals(log.operation_ParentId, StringComparison.OrdinalIgnoreCase)).duration.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { traceIds.SingleOrDefault(t => t.traceId.Equals(log.operation_ParentId, StringComparison.OrdinalIgnoreCase)).duration.ToString("F2") + "ms / " + $"{(traceIds.SingleOrDefault(t => t.traceId.Equals(log.operation_ParentId, StringComparison.OrdinalIgnoreCase)).duration / 1000.00).ToString("F1", CultureInfo.InvariantCulture)}s" } }
                                        })
                                    }
                                }
                             );

                    ConsoleRenderer.RenderDocument(doc);
                }
                else
                {
                    ColorConsole.WriteLine("\nNo logs found!".Yellow());
                }
            }
        }

        private async Task<(int index, string endpoint, string traceId, double duration)[]> Execute(string endpoint, float topIndex, int interations)
        {
            var result = await Task.WhenAll(Enumerable.Range(0, interations).Select(async i =>
            {
                var traceId = Guid.NewGuid().ToString();
                var response = await ProcessRequest(endpoint, traceId);
                string result = response.status;
                await Lock.WaitAsync();
                Log(endpoint, topIndex, i, traceId, response.duration, result);
                Lock.Release();
                return (i, endpoint, traceId, response.duration);
            }));

            return result;
        }

        private void Log(string endpoint, float topIndex, int i, string traceId, double duration, string result)
        {
            var sec = (int)Math.Round(duration / 1000);
            var bar = string.Empty.PadLeft(sec > 1 ? sec : 1, '.');
            var id = $"{topIndex}.{i + 1}";
            var coloredBar = bar.OnGreen();
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

            ColorConsole.WriteLine($"{id} ", endpoint.Green(), "\n",
                "stat".PadLeft(id.Length + 5).Green(), $": {result}", "\n",
                "opid".PadLeft(id.Length + 5).Green(), ": ", traceId, "\n",
                "time".PadLeft(id.Length + 5).Green(), ": ", duration.ToString("F2", CultureInfo.InvariantCulture), "ms".Green(), " (~", (duration / 1000.00).ToString("F1", CultureInfo.InvariantCulture), "s".Green(), ") ", coloredBar, "\n");
        }

        private async Task<(string status, double duration)> ProcessRequest(string endpoint, string traceId)
        {
            // See: https://docs.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps
            // Credit: https://josefottosson.se/you-are-probably-still-using-httpclient-wrong-and-it-is-destabilizing-your-software/
            var elapsedTime = 0.00;
            var result = string.Empty;
            try
            {
                var token = this.settings.Token;
                this.client.DefaultRequestHeaders.Clear();
                this.client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
                this.client.DefaultRequestHeaders.Add(RequestId, traceId);
                var taskWatch = Stopwatch.StartNew();
                var response = await this.client.SendAsync(new HttpRequestMessage(HttpMethod.Get, endpoint), HttpCompletionOption.ResponseHeadersRead);
                elapsedTime = taskWatch.ElapsedMilliseconds;
                result = $"{(int)response.StatusCode}: {response.ReasonPhrase}";
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
                ColorConsole.WriteLine(ex.Message.White().OnRed());
            }

            return (result, elapsedTime);
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
