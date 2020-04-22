namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using ByteSizeLib;

    using CsvHelper.Configuration.Attributes;

    using Newtonsoft.Json;

    public class Result
    {
        public float id { get; set; }
        public DateTime timestamp { get; set; }
        public string url { get; set; }
        public string query => this.details?.Query;
        public string result { get; set; }
        public long? size_b { get; set; }
        public string size_str => size_b.HasValue ? $"{size_num_str}{size_unit}" : string.Empty;
        public double local_ms { get; set; }
        public double ai_ms { get; set; }
        public double local_s => Math.Round(this.local_ms / 1000, 2);
        public double ai_s => Math.Round(this.ai_ms / 1000, 2);
        public string op_Id { get; set; }
        public string ai_op_Id { get; set; }
        public double exp_sla_s { get; set; }

        [Ignore, JsonIgnore]
        public long run_Id { get; set; }

        [Ignore, JsonIgnore]
        public string full_url => this.url + (string.IsNullOrWhiteSpace(this.details?.Query) ? string.Empty : this.details?.Query);

        [Ignore, JsonIgnore]
        public Endpoint details { get; set; }

        [Ignore, JsonIgnore]
        public string size_num_str => size_b.HasValue ? ByteSize.FromBytes(size_b.Value).LargestWholeNumberDecimalValue.ToString("F2") : string.Empty;

        [Ignore, JsonIgnore]
        public string size_unit => size_b.HasValue ? $"{ByteSize.FromBytes(size_b.Value).LargestWholeNumberDecimalSymbol}" : string.Empty;

        [Ignore, JsonIgnore]
        public double duration_ms => string.IsNullOrEmpty(ai_op_Id) ? local_ms : ai_ms;

        [Ignore, JsonIgnore]
        public string duration_ms_str => this.duration_ms.ToString("F2");

        [Ignore, JsonIgnore]
        public double duration_s => this.duration_ms / 1000;

        [Ignore, JsonIgnore]
        public int duration_s_round => (int)Math.Round(this.duration_s);

        [Ignore, JsonIgnore]
        public string duration_s_str => this.duration_s.ToString("F2");

        [Ignore, JsonIgnore]
        public string local_ms_str => this.local_ms.ToString("F2");

        [Ignore, JsonIgnore]
        public string local_s_str => this.local_s.ToString("F2");

        [Ignore, JsonIgnore]
        public string ai_ms_str => this.ai_ms.ToString("F2");

        [Ignore, JsonIgnore]
        public string ai_s_str => this.ai_s.ToString("F2");

        [Ignore, JsonIgnore]
        public List<PropertyInfo> Properties { get; } = typeof(Result).GetProperties().Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null).ToList();
    }
}
