namespace Perfx
{
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    // Credits: https://www.stevejgordon.co.uk/httpclientfactory-asp-net-core-logging, https://www.stevejgordon.co.uk/httpclientfactory-aspnetcore-outgoing-request-middleware-pipeline-delegatinghandlers
    public class TimingHandler : DelegatingHandler
    {
        private readonly ILogger<TimingHandler> logger;

        public TimingHandler(ILogger<TimingHandler> logger)
        {
            this.logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri.OriginalString;
            var traceId = request.Headers.GetValues(HttpService.RequestId).FirstOrDefault();
            logger.LogInformation($"Begin: {uri} ({traceId})");
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                return response;
            }
            finally
            {
                logger.LogInformation($"Finish: {uri} ({traceId}): {sw.ElapsedMilliseconds}ms");
            }
        }
    }
}
