namespace Perfx
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

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
        private static readonly List<PropertyInfo> props = typeof(LogRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
        private readonly ILogger<LogDataService> logger;
        private Settings settings;

        public LogDataService(IOptionsMonitor<Settings> settingsMonitor, ILogger<LogDataService> logger)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.logger = logger;
        }

        public async Task<IEnumerable<LogRecord>> GetLogs(IEnumerable<string> traceIds, string timeframe = "60m")
        {
            var subquery = "and (" + string.Join(" ", traceIds.Select((t, i) => (i == 0 ? string.Empty : "or ") + $"* contains '{t}'")) + ") ";
            var query = string.Format(Query, timeframe, subquery);
            logger.LogInformation(query);
            var req = string.Format(Url, this.settings.AppInsightsAppId, query);
            var response = await req.WithHeader("x-api-key", this.settings.AppInsightsApiKey)
                .WithHeader("Accept", "application/json")
                .WithHeader("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
                .PostJsonAsync(new { query })
                .ReceiveJson<LogData>();

            var results = new List<LogRecord>();
            var cols = response.tables?.FirstOrDefault()?.columns?.ToList();
            var rows = response.tables?.FirstOrDefault()?.rows?.ToList();
            if (cols?.Count > 0 && rows?.Count > 0)
            {
                for (int r = 0; r < rows.Count; r++)
                {
                    var record = new LogRecord();
                    results.Add(record);
                    for (int c = 0; c < cols.Count; c++)
                    {
                        var prop = props[c];
                        prop.SetValue(record, rows[r][c]);
                    }
                }
            }

            return results;
        }
    }
}
