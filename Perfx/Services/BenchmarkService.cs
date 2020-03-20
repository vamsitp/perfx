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
    using Perfx;
    using SmartFormat;

    public class BenchmarkService : IDisposable
    {
        private static int leftPadding;

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
            leftPadding = (this.settings.Endpoints.Count().ToString() + this.settings.Endpoints.Count().ToString()).Length + 6;
        }

        public async Task<List<Result>> Execute(int? iterations = null, CancellationToken stopToken = default)
        {
            ColorConsole.WriteLine("Endpoints: ", settings.Endpoints.Count().ToString().Green());
            ColorConsole.WriteLine("Iterations: ", iterations?.ToString()?.Green() ?? settings.Iterations.ToString().Green(), "\n");

            var endpointRecords = this.settings.Endpoints.SelectMany((endpoint, groupIndex) =>
            {
                return Enumerable.Range(0, iterations ?? settings.Iterations).Select(i =>
                    new Result
                    {
                        id = float.Parse($"{groupIndex + 1}.{i + 1}"),
                        op_Id = Guid.NewGuid().ToString(),
                        url = this.settings.FormatArgs == null ? endpoint : Smart.Format(endpoint, this.settings.FormatArgs)
                    });
            });

            var endpointDetails = await GetEndpointDetails();
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

        private async Task<List<Endpoint>> GetEndpointDetails()
        {
            List<Endpoint> endpointDetails;

            try
            {
                if (this.plugin != null)
                {
                    endpointDetails = await this.plugin.GetEndpointDetails(this.settings);
                }
                else
                {
                    endpointDetails = ResultsFileExtensions.ReadFromExcel<Endpoint>(settings.InputsFile, "Inputs");
                }
            }
            catch (Exception ex) when (ex is NotImplementedException || ex is NotSupportedException)
            {
                endpointDetails = ResultsFileExtensions.ReadFromExcel<Endpoint>(settings.InputsFile, "Inputs");
            }

            return endpointDetails;
        }

        private static void SetInput(Result record, List<Endpoint> detailsList)
        {
            Endpoint details;

            // TODO: Find a better way to extract the decimal-part whole-number
            var id = Convert.ToInt32(record.id.ToString().Split('.').LastOrDefault());
            if (detailsList?.Count > 0)
            {
                if (detailsList.Count >= id)
                {
                    details = detailsList[id - 1];
                }
                else
                {
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
            ColorConsole.WriteLine($"{id} ".PadLeft(leftPadding - 4), result.url.Green(), "\n",
                "opid".PadLeft(leftPadding).Green(), ": ", result.op_Id, "\n",
                "stat".PadLeft(leftPadding).Green(), ": ", result.result.GetColorToken(" "), " ", result.result, "\n",
                "time".PadLeft(leftPadding).Green(), ": ", result.local_ms.GetColorToken(" "), " ", result.local_ms_str, "ms".Green(), " (~", result.local_s_str, "s".Green(), ") ", "\n",
                "size".PadLeft(leftPadding).Green(), ": ", result.size_b.GetColorToken(" "), " ", result.size_num_str, result.size_unit.Green(),  "\n");
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
