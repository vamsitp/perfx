namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using ByteSizeLib;

    using CsvHelper.Configuration.Attributes;

    using MathNet.Numerics.Statistics;

    using Newtonsoft.Json;

    public class Run
    {
        public Run(IEnumerable<Result> results, string url)
        {
            this.results = results;
            this.url = url;
        }

        public string url { get; set; }
        public double dur_min_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.Min(), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double dur_max_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.Max(), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double dur_mean_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.Mean(), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double dur_median_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.Median(), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double dur_std_dev_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.StandardDeviation(), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double dur_90_perc_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.Percentile(90), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double dur_95_perc_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.Percentile(95), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double dur_99_perc_s => this.ok_results_durations_ms.Count > 1 ? Math.Round(this.ok_results_durations_ms.Percentile(99), 2) : this.ok_results_durations_ms.FirstOrDefault();
        public double size_min_kb => this.ok_results_size_kb.Count > 1 ? Math.Round(this.ok_results_size_kb.Min(), 2) : this.ok_results_size_kb.FirstOrDefault();
        public double size_max_kb => this.ok_results_size_kb.Count > 1 ? Math.Round(this.ok_results_size_kb.Max(), 2) : this.ok_results_size_kb.FirstOrDefault();
        public double ok_200 => (int)Math.Round(((double)(this.ok_results.Count() / this.results.Count())) * 100);
        public double other_xxx => 100 - this.ok_200;

        [Ignore, JsonIgnore]
        public IEnumerable<Result> results { get; set; }

        [Ignore, JsonIgnore]
        public List<Result> ok_results => this.results.Where(x => x.result.Contains("200"))?.ToList();

        [Ignore, JsonIgnore]
        public List<double> ok_results_durations_ms => this.ok_results.Select(x => x.duration_ms / 1000)?.ToList();

        [Ignore, JsonIgnore]
        public List<double> ok_results_size_kb => this.ok_results.Select(x => x.size_b.HasValue ? ByteSize.FromBytes(x.size_b.Value).KiloBytes : 0)?.ToList();

        [Ignore, JsonIgnore]
        public string run_by { get; } = Extensions.UserName;

        [Ignore, JsonIgnore]
        public List<PropertyInfo> Properties { get; } = typeof(Run).GetProperties().Where(p => !p.Name.Equals(nameof(url)) && p.GetCustomAttribute<IgnoreAttribute>() == null).ToList();
    }
}
