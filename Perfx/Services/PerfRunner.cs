namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    public class PerfRunner : IDisposable
    {
        private readonly LogDataService logDataService;
        private readonly IPlugin plugin;
        private readonly HttpService httpService;

        private bool disposedValue = false;

        private Settings settings;

        private static int leftPadding;

        public PerfRunner(IOptionsMonitor<Settings> settingsMonitor, LogDataService logDataService, IServiceProvider services, HttpService httpService)
        {
            this.httpService = httpService;
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.logDataService = logDataService;
            this.plugin = services.GetService<IPlugin>();
            leftPadding = (this.settings.Endpoints.Count().ToString() + this.settings.Endpoints.Count().ToString()).Length + 5;
        }

        public async Task<List<Result>> Execute(int? iterations = null, CancellationToken stopToken = default)
        {
            ColorConsole.WriteLine("Endpoints: ", settings.Endpoints.Count().ToString().Green());
            ColorConsole.WriteLine("Iterations: ", iterations?.ToString()?.Green() ?? settings.Iterations.ToString().Green(), "\n");

            var endpointRecords = settings.Endpoints.SelectMany((endpoint, groupIndex) =>
            {
                return Enumerable.Range(0, iterations ?? settings.Iterations).Select(i =>
                    new Result
                    {
                        id = float.Parse($"{groupIndex + 1}.{i + 1}"),
                        op_Id = Guid.NewGuid().ToString(),
                        url = endpoint
                    });
            });

            List<Endpoint> endpointDetails = null;
            try
            {
                if (this.plugin != null)
                {
                    endpointDetails = await this.plugin.GetEndpointDetails(this.settings);
                }
                else
                {
                    endpointDetails = ResultsHelper.ReadFromExcel<Endpoint>(settings.InputsFile, "Inputs");
                }
            }
            catch (Exception ex) when (ex is NotImplementedException || ex is NotSupportedException)
            {
                endpointDetails = ResultsHelper.ReadFromExcel<Endpoint>(settings.InputsFile, "Inputs");
            }

            var groupedDetails = endpointDetails?.GroupBy(input => input.Url);

            // Group by endpoint?
            var endpointTasks = endpointRecords.Select((record, i) =>
            {
                var details = groupedDetails?.SingleOrDefault(x => x != null && x.Key?.Equals(record.url, StringComparison.OrdinalIgnoreCase) == true)?.ToList();
                SetInput(record, details);
                return Execute(record, stopToken);
            });

            var results = await Task.WhenAll(endpointTasks);
            return results.ToList();
        }

        private static void SetInput(Result record, List<Endpoint> details)
        {
            Endpoint input;

            // TODO: Find a better way to extract the decimal-part whole-number
            var id = Convert.ToInt32(record.id.ToString().Split('.').LastOrDefault());
            if (details?.Count > 0)
            {
                if (details.Count >= id)
                {
                    input = details[id - 1];
                }
                else
                {
                    input = details.FirstOrDefault();
                }
            }
            else
            {
                input = new Endpoint { Method = nameof(HttpMethod.Get) };
            }

            record.input = input;
        }

        private async Task<Result> Execute(Result record, CancellationToken stopToken = default)
        {
            await this.httpService.ProcessRequest(record, stopToken);
            // await Lock.WaitAsync();
            Log(record);
            // Lock.Release();
            return record;
        }

        private void Log(Result result)
        {
            var id = result.id.ToString();
            ColorConsole.WriteLine($"{id} ".PadLeft(leftPadding - 4), result.url.Green(), "\n",
                "opid".PadLeft(leftPadding).Green(), ": ", result.op_Id, "\n",
                "stat".PadLeft(leftPadding).Green(), ": ", result.result.GetColorToken(" "), " ", result.result, "\n",
                "time".PadLeft(leftPadding).Green(), ": ", result.local_ms.GetColorToken(" "), " ", result.local_ms_str, "ms".Green(), " (~", result.local_s_str, "s".Green(), ") ", "\n",
                "size".PadLeft(leftPadding).Green(), ": ", result.size_b.GetColorToken(" "), " ", result.size_num_str, result.size_unit.Green(),  "\n");
        }

        public async Task ExecuteAppInsights(List<Result> results, string timeframe = "60m", int retries = 60, CancellationToken stopToken = default)
        {
            if (!string.IsNullOrWhiteSpace(settings.AppInsightsAppId) && !string.IsNullOrWhiteSpace(settings.AppInsightsApiKey))
            {
                ColorConsole.Write(" App-Insights ".White().OnDarkGreen(), "[", results.Count.ToString().Green(), "]");
                var found = false;
                var i = 0;
                List<Log> aiLogs = null;
                do
                {
                    i++;
                    aiLogs = (await logDataService.GetLogs(results.Select(t => t.op_Id), stopToken, timeframe))?.ToList();
                    found = aiLogs?.Count >= results.Count;
                    ColorConsole.Write((aiLogs?.Count > 0 ? $"{aiLogs?.Count.ToString()}" : string.Empty), ".".Green());
                    await Task.Delay(1000);
                }
                while (!stopToken.IsCancellationRequested && found == false && i < retries);

                if (aiLogs?.Count > 0)
                {
                    aiLogs.ForEach(ai =>
                    {
                        var result = results.SingleOrDefault(t => t.op_Id.Equals(ai.operation_ParentId, StringComparison.OrdinalIgnoreCase));
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
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.httpService.Dispose();
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
