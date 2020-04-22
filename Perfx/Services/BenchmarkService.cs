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

    using SmartFormat;

    public class BenchmarkService : IDisposable
    {
        private const string Inputs = "Inputs";

        private static int leftPadding;
        private readonly ExcelOut excelOutput;

        private readonly IPlugin plugin;
        private readonly HttpService httpService;
        private bool disposedValue = false;
        private Settings settings;

        public BenchmarkService(IOptionsMonitor<Settings> settingsMonitor, IServiceProvider services, HttpService httpService)
        {
            this.httpService = httpService;
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => this.settings = changedSettings);
            this.plugin = services.GetService<IPlugin>();
            this.excelOutput = new ExcelOut();
            leftPadding = (this.settings.Endpoints.Count().ToString() + this.settings.Endpoints.Count().ToString()).Length + 6;
        }

        public async Task<List<Result>> Execute(int? iterations = null, CancellationToken stopToken = default)
        {
            ColorConsole.WriteLine("Endpoints: ", settings.Endpoints.Count().ToString().Green());
            ColorConsole.WriteLine("Iterations: ", iterations?.ToString()?.Green() ?? settings.Iterations.ToString().Green(), "\n");

            var endpointRecords = this.settings.Endpoints.SelectMany((endpoint, groupIndex) =>
            {
                return Enumerable.Range(0, iterations ?? this.settings.Iterations).Select(i =>
                    {
                        var epWithSla = this.GetFormattedUrl(endpoint).Split(new[] { "::" }, 2, StringSplitOptions.None);
                        var slas = epWithSla.Length > 1 && !string.IsNullOrEmpty(epWithSla.FirstOrDefault()) ? epWithSla.FirstOrDefault().Split(new[] { "|" }, 2, StringSplitOptions.None) : null;
                        var result = new Result
                        {
                            id = float.Parse($"{groupIndex + 1}.{i + 1}"),
                            op_Id = Guid.NewGuid().ToString(),
                            url = epWithSla.LastOrDefault(),
                            sla_dur_s = slas?.Length > 0 && !string.IsNullOrEmpty(slas.FirstOrDefault()) ? double.Parse(slas.FirstOrDefault()) : this.settings.ResponseTimeSla,
                            sla_size_kb = slas?.Length > 1 && !string.IsNullOrEmpty(slas.LastOrDefault()) ? double.Parse(slas.LastOrDefault()) : this.settings.ResponseSizeSla
                        };
                        return result;
                    });
            });

            var endpointDetails = (await GetEndpointDetails())?.Where(e => !string.IsNullOrWhiteSpace(e.Url));
            var groupedDetails = endpointDetails?.GroupBy(input => this.GetFormattedUrl(input.Url));

            // Group by endpoint?
            var endpointTasks = endpointRecords.Select((record, i) =>
            {
                var details = groupedDetails?.SingleOrDefault(x => x != null && x.Key?.Trim()?.Equals(record.url.Trim(), StringComparison.OrdinalIgnoreCase) == true)?.ToList();
                SetInput(record, details);
                return Execute(record, stopToken);
            });

            var results = await Task.WhenAll(endpointTasks);
            return results.ToList();
        }

        private string GetFormattedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            return this.settings.FormatArgs == null ? url : Smart.Format(url, this.settings.FormatArgs);
        }

        private async Task<IList<Endpoint>> GetEndpointDetails()
        {
            IList<Endpoint> endpointDetails;

            try
            {
                if (this.plugin != null)
                {
                    endpointDetails = await this.plugin.GetEndpointDetails(this.settings);
                }
                else
                {
                    endpointDetails = await this.excelOutput.Read<Endpoint>(settings.InputsFile, Inputs);
                }
            }
            catch (Exception ex) when (ex is NotImplementedException || ex is NotSupportedException)
            {
                endpointDetails = await this.excelOutput.Read<Endpoint>(settings.InputsFile, Inputs);
            }

            return endpointDetails;
        }

        private static void SetInput(Result record, List<Endpoint> detailsList)
        {
            Endpoint details;

            // TODO: Find a better way to extract the decimal-part whole-number
            var id = Convert.ToInt32(record.id.ToString().Split('.').LastOrDefault().Trim());
            if (detailsList?.Count > 0)
            {
                if (detailsList.Count >= id)
                {
                    details = detailsList[id - 1];
                }
                else
                {
                    // TODO: Random instead of FirstOrDefault?
                    details = detailsList.FirstOrDefault();
                }
            }
            else
            {
                details = new Endpoint { Method = nameof(HttpMethod.Get) };
            }

            record.details = details;
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
            ColorConsole.WriteLine($"{id} ".PadLeft(leftPadding - 4), result.full_url.Green(), $" (".White(), $"sla: {result.sla_dur_s}s".DarkGray(), " / ".White(), $"{result.sla_size_kb}Kb".DarkGray(), ")".White(), "\n",
                "opid".PadLeft(leftPadding).Green(), ": ", result.op_Id, "\n",
                "stat".PadLeft(leftPadding).Green(), ": ", result.result.GetColorTokenForStatusCode(" "), " ", result.result, "\n",
                "time".PadLeft(leftPadding).Green(), ": ", result.local_ms.GetColorTokenForDuration(result.sla_dur_s,  " "), " ", result.local_ms_str, "ms".Green(), " (~", result.local_s_str, "s".Green(), ") ", "\n",
                "size".PadLeft(leftPadding).Green(), ": ", result.size_b.GetColorTokenForSize(result.sla_size_kb, " "), " ", result.size_num_str, result.size_unit.Green(),  "\n");
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
