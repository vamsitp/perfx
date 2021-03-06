﻿namespace Perfx
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
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

        // See: https://docs.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps
        // Credit: https://josefottosson.se/you-are-probably-still-using-httpclient-wrong-and-it-is-destabilizing-your-software/
        public async Task<Result> ProcessRequest(Result record, CancellationToken stopToken = default)
        {
            var token = this.settings.Token;
            var details = record.details;
            this.client.DefaultRequestHeaders.Clear();

            var headers = details.Headers?.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)?.Select(x => x?.Split(':', 2))?.ToDictionary(split => split[0]?.Trim().ToLowerInvariant(), split => split[1]?.Trim(), StringComparer.OrdinalIgnoreCase);
            if (!(headers?.ContainsKey(AuthHeader) == true))
            {
                this.client.DefaultRequestHeaders.Add(AuthHeader, Bearer + token);
            }

            if (!(headers?.ContainsKey(RequestId) == true))
            {
                this.client.DefaultRequestHeaders.Add(RequestId, record.op_Id);
            }

            if (headers?.Count > 0)
            {
                foreach (var header in headers)
                {
                    this.client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            using (var request = new HttpRequestMessage(new HttpMethod(details.Method), record.full_url))
            {
                var content = record.details.Body?.Split(':', 2);
                if (content?.Length > 0)
                {
                    using (var httpContent = new StringContent(content[1]?.Trim(), Encoding.UTF8, content[0]?.Trim()))
                    {
                        request.Content = httpContent;
                        await ProcessRequest(record, request, stopToken);
                    }
                }
                else
                {
                    await ProcessRequest(record, request, stopToken);
                }
            }

            return record;
        }

        private async Task ProcessRequest(Result record, HttpRequestMessage request, CancellationToken stopToken)
        {
            HttpResponseMessage response = null;
            var completion = this.settings.ReadResponseHeadersOnly ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
            record.timestamp = DateTime.Now;
            var taskWatch = Stopwatch.StartNew();
            try
            {
                response = await this.client.SendAsync(request, completion, stopToken);
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
            finally
            {
                response?.Dispose();
            }
        }

        public async Task<T> GetAsync<T>(string url)
        {
            var response = await this.client.GetAsync(url);
            var result = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<T>(result);
            return json;
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
