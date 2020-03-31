namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    // Credit: https://codereview.stackexchange.com/a/18679
    public class AsyncProcessor
    {
        public const string RequestId = "Request-Id";
        private const string OperationId = "operation_Id";
        private const string AuthHeader = "Authorization";
        private const string Bearer = "Bearer ";

        private readonly HttpClient client;
        private readonly ILogger<AsyncProcessor> logger;
        private Settings settings;

        public AsyncProcessor(IHttpClientFactory httpClientFactory, IOptionsMonitor<Settings> settingsMonitor, ILogger<AsyncProcessor> logger)
        {
            this.client = httpClientFactory.CreateClient(nameof(Perfx));
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.logger = logger;
        }

        public ISourceBlock<Result> ProcessEndpoints(string[] endpoints, int topIndex, int maxDegreeOfParallelism, CancellationToken stopToken)
        {
            var block = new TransformManyBlock<(string endpoint, int i), Result>(
                async item =>
                {
                    var result = new Result { id = float.Parse($"{topIndex}.{item.i + 1}"), op_Id = Guid.NewGuid().ToString(), url = item.endpoint };
                    return await ProcessEndpoint(result, stopToken);
                }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism });

            for (var i = 0; i < endpoints.Length; i++)
            {
                var endpoint = endpoints[i];
                block.Post((endpoint, i));
            }

            block.Complete();
            return block;
        }

        private async Task<Result[]> ProcessEndpoint(Result result, CancellationToken stopToken)
        {
            var token = this.settings.Token;
            this.client.DefaultRequestHeaders.Clear();
            this.client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
            this.client.DefaultRequestHeaders.Add(RequestId, result.op_Id);
            result.timestamp = DateTime.Now;
            var taskWatch = Stopwatch.StartNew();

            try
            {
                // See: https://docs.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps
                // Credit: https://josefottosson.se/you-are-probably-still-using-httpclient-wrong-and-it-is-destabilizing-your-software/
                var response = await this.client.SendAsync(new HttpRequestMessage(HttpMethod.Get, result.url), this.settings.ReadResponseHeadersOnly ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, stopToken);
                result.local_ms = taskWatch.ElapsedMilliseconds;
                result.result = $"{(int)response.StatusCode}: {response.ReasonPhrase}";
                result.size_b = response.Content.Headers.ContentLength;
            }
            catch (Exception ex)
            {
                result.local_ms = taskWatch.ElapsedMilliseconds;
                result.result = ex.Message;
                // ColorConsole.WriteLine(string.Empty.PadLeft(leftPadding), ex.Message.White().OnRed(), ": ", record.url.DarkGray(), $" (", record.op_Id.DarkGray(), ")");
                this.logger.LogTrace($"ERR: {ex.Message}: {result.id} - {result.url} ({result.op_Id})");
            }

            return new[] { result };
        }

        // Credit: https://stackoverflow.com/a/46383356
        public async Task<List<Result>> ProcessDownloads(string[] endpoints, int iterations, CancellationToken stopToken)
        {
            //var result = new List<Result>();
            //var downloadData = new TransformBlock<string, HttpResponseMessage>(async url =>
            //{
            //    var record = new Record { id = float.Parse($"{topIndex}.{i + 1}"), op_Id = Guid.NewGuid().ToString(), url = url };
            //    var token = this.settings.Token;
            //    this.client.DefaultRequestHeaders.Clear();
            //    this.client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
            //    this.client.DefaultRequestHeaders.Add(RequestId, record.op_Id);
            //    record.timestamp = DateTime.Now;
            //    var taskWatch = Stopwatch.StartNew();
            //    var response = await this.client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), this.settings.ReadResponseHeadersOnly ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, stopToken);
            //    return response;
            //}, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = concurrentDownloads });

            //var processData = new TransformBlock<HttpResponseMessage, Result>(
            //    response =>
            //    {
            //        record.local_ms = taskWatch.ElapsedMilliseconds;
            //        record.result = $"{(int)response.StatusCode}: {response.ReasonPhrase}";
            //        record.size_b = response.Content.Headers.ContentLength;
            //        return record;
            //    },
            //    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });

            //var collectData = new ActionBlock<Result>(data => result.Add(data)); // When you don't specify options dataflow processes items one at a time.

            //// Set up the chain of blocks, have it call `.Complete()` on the next block when the current block finishes processing it's last item.
            //downloadData.LinkTo(processData, new DataflowLinkOptions { PropagateCompletion = true });
            //processData.LinkTo(collectData, new DataflowLinkOptions { PropagateCompletion = true });

            //// Load the data in to the first transform block to start off the process.
            //for (var i = 0; i < uris.ToArray().Length; i++)
            //{
            //    var uri = uris.ElementAt(i);
            //    await downloadData.SendAsync(uri).ConfigureAwait(false);
            //}

            //downloadData.Complete(); //Signal you are done adding data.

            //// Wait for the last object to be added to the list.
            //await collectData.Completion.ConfigureAwait(false);

            //return result;

            return await Task.FromResult(default(List<Result>));
        }
    }
}
