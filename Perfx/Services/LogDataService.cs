namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Flurl.Http;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class LogDataService
    {
        // https://dev.applicationinsights.io/quickstart
        // https://dev.loganalytics.io/documentation/Authorization/API-Keys
        // https://api.applicationinsights.io/{version}/apps/{app-id}/{operation}/[path]?[parameters]
        private const string Query = "requests " +
            "| where timestamp between(ago({0}) .. now()) {1}" +
            "| order by timestamp asc " +
            "| project id=row_number(), timestamp, url, duration, operation_Id, operation_ParentId, resultCode, performanceBucket, client_IP, client_City";
        private const string Url = "https://api.applicationinsights.io/v1/apps/{0}/query";
        private static readonly List<PropertyInfo> props = typeof(Log).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
        private readonly ILogger<LogDataService> logger;
        private Settings settings;

        public LogDataService(IOptionsMonitor<Settings> settingsMonitor, ILogger<LogDataService> logger)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.logger = logger;
        }

        public async Task ExecuteAppInsights(IList<Result> results, string timeframe = "60m", int retries = 60, CancellationToken stopToken = default)
        {
            if (!string.IsNullOrWhiteSpace(settings.AppInsightsAppId) && !string.IsNullOrWhiteSpace(settings.AppInsightsApiKey))
            {
                try
                {
                    ColorConsole.Write(" App-Insights ".White().OnDarkGreen(), "[", results.Count.ToString().Green(), "]");
                    var found = false;
                    var i = 0;
                    List<Log> aiLogs = null;
                    do
                    {
                        i++;
                        aiLogs = (await this.GetLogs(results.Select(t => t.op_Id), stopToken, timeframe))?.ToList();
                        found = aiLogs?.Count >= results.Count;
                        ColorConsole.Write((aiLogs?.Count > 0 ? $"{aiLogs?.Count.ToString()}" : string.Empty), ".".Green());
                        await Task.Delay(1000);
                    }
                    while (!stopToken.IsCancellationRequested && found == false && i < retries);

                    if (aiLogs?.Count > 0)
                    {
                        aiLogs.ForEach(ai =>
                        {
                            var result = results.SingleOrDefault(r => ai.operation_ParentId.Contains(r.op_Id, StringComparison.OrdinalIgnoreCase));
                            result.ai_ms = ai.duration;
                            result.ai_op_Id = ai.operation_Id;
                        // TODO: Rest of the props
                    });
                        ColorConsole.WriteLine();
                    }
                    else
                    {
                        ColorConsole.WriteLine("\nNo logs found!".Yellow());
                    }
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.White().OnRed());
                }
            }
        }

        public async Task<IEnumerable<Log>> GetLogs(IEnumerable<string> traceIds, CancellationToken stopToken = default, string timeframe = "60m")
        {
            var subquery = "and (" + string.Join(" ", traceIds.Select((t, i) => (i == 0 ? string.Empty : "or ") + $"* contains '{t}'")) + ") ";
            var query = string.Format(Query, timeframe, subquery);
            logger.LogInformation(query);
            var req = string.Format(Url, this.settings.AppInsightsAppId, query);
            var response = await req.WithHeader("x-api-key", this.settings.AppInsightsApiKey)
                .WithHeader("Accept", "application/json")
                .WithHeader("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
                .PostJsonAsync(new { query }, stopToken)
                .ReceiveJson<LogData>();

            var results = new List<Log>();
            var cols = response.tables?.FirstOrDefault()?.columns?.ToList();
            var rows = response.tables?.FirstOrDefault()?.rows?.ToList();
            if (cols?.Count > 0 && rows?.Count > 0)
            {
                for (int r = 0; r < rows.Count; r++)
                {
                    var log = new Log();
                    results.Add(log);
                    for (int c = 0; c < cols.Count; c++)
                    {
                        var prop = props[c];
                        prop.SetValue(log, rows[r][c]);
                    }
                }
            }

            return results;
        }
    }
}
