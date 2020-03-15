namespace Perfx
{
    using System;
    using System.Collections.Generic;

    public class Record
    {
        public float id { get; set; }
        public string url { get; set; }
        public double duration_ms { get; set; }
        public string traceId { get; set; }
        public string result { get; set; }
        public double ai_duration_ms { get; set; }
        public string ai_op_Id { get; set; }

        public int GetDurationInSec(bool forAppInsights = false)
        {
            var sec = (int)Math.Round((forAppInsights ? this.ai_duration_ms : this.duration_ms) / 1000);
            return sec >= 1 ? sec : 1;
        }

        public string GetDurationString(bool forAppInsights = false, bool suffixUnit = false) => (forAppInsights ? this.ai_duration_ms : this.duration_ms).ToString("F2") + (suffixUnit ? "ms" : string.Empty);

        public string GetDurationInSecString(bool forAppInsights = false, bool suffixUnit = false) => ((forAppInsights ? this.ai_duration_ms : this.duration_ms) / 1000).ToString("F1") + (suffixUnit ? "s" : string.Empty);
    }

    public class Run
    {
        public DateTime timestamp { get; set; }
        public List<Record> Records { get; set; }
    }
}
