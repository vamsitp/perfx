namespace Perfx
{
    using System.Diagnostics;
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
            var sw = Stopwatch.StartNew();
            logger.LogWarning($"START: {uri}");
            var response = await base.SendAsync(request, cancellationToken);
            logger.LogWarning($"FINISH: {uri} ({sw.ElapsedMilliseconds}ms)");
            return response;
        }
    }
}
