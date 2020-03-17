namespace Perfx
{
    using System;
    using System.Collections.Generic;

    using ByteSizeLib;
    using ColoredConsole;
    using CsvHelper.Configuration.Attributes;

    public class Record
    {
        public float id { get; set; }
        public DateTime timestamp { get; set; }
        public string url { get; set; }
        public string op_Id { get; set; }
        public string result { get; set; }
        public long? size { get; set; }
        public string size_str => size.HasValue ? $"{size_num_str}{size_unit}" : string.Empty;

        [Ignore]
        public string size_num_str => size.HasValue ? ByteSize.FromBytes(size.Value).LargestWholeNumberDecimalValue.ToString("F2") : string.Empty;

        [Ignore]
        public string size_unit => size.HasValue ? $"{ByteSize.FromBytes(size.Value).LargestWholeNumberDecimalSymbol}" : string.Empty;

        [Ignore]
        public double duration_ms => string.IsNullOrEmpty(ai_op_Id) ? local_ms : ai_ms;

        [Ignore]
        public string duration_ms_str => this.duration_ms.ToString("F2");

        [Ignore]
        public double duration_s => this.duration_ms / 1000;

        [Ignore]
        public int duration_s_round => (int)Math.Round(this.duration_s);

        [Ignore]
        public string duration_s_str => this.duration_s.ToString("F1");

        public double local_ms { get; set; }

        [Ignore]
        public string local_ms_str => this.local_ms.ToString("F2");
        public double local_s => this.local_ms / 1000;

        [Ignore]
        public string local_s_str => this.local_s.ToString("F1");

        public string ai_op_Id { get; set; }
        public double ai_ms { get; set; }

        [Ignore]
        public string ai_ms_str => this.ai_ms.ToString("F2");
        public double ai_s => this.ai_ms / 1000;

        [Ignore]
        public string ai_s_str => this.ai_s.ToString("F1");
    }

    public class Run
    {
        public DateTime timestamp { get; set; }
        public List<Record> Records { get; set; }
    }
}
