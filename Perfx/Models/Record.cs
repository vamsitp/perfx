namespace Perfx
{
    using System;
    using System.Collections.Generic;

    public class Record
    {
        public float id { get; set; }
        public string url { get; set; }

        public double duration_ms => string.IsNullOrEmpty(ai_op_Id) ? local_ms : ai_ms;
        public string duration_ms_str => this.duration_ms.ToString("F2");
        public double duration_s => this.duration_ms / 1000;
        public int duration_s_round => (int)Math.Round(this.duration_s);
        public string duration_s_str => this.duration_s.ToString("F1");

        public double local_ms { get; set; }
        public string local_ms_str => this.local_ms.ToString("F2");
        public double local_s => this.local_ms / 1000;
        public string local_s_str => this.local_s.ToString("F1");

        public string traceId { get; set; }
        public string result { get; set; }

        public string ai_op_Id { get; set; }
        public double ai_ms { get; set; }
        public string ai_ms_str => this.ai_ms.ToString("F2");
        public double ai_s => this.ai_ms / 1000;

        public string ai_s_str => this.ai_s.ToString("F1");
    }

    public class Run
    {
        public DateTime timestamp { get; set; }
        public List<Record> Records { get; set; }
    }
}
