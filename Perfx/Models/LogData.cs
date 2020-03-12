namespace Perfx
{
    using System;

    public class LogData
    {
        public Table[] tables { get; set; }
    }

    public class Table
    {
        public string name { get; set; }
        public Column[] columns { get; set; }
        public object[][] rows { get; set; }
    }

    public class Column
    {
        public string name { get; set; }
        public string type { get; set; }
    }

    public class LogRecord
    {
        public long id { get; set; }
        public DateTime timestamp { get; set; }
        public string url { get; set; }
        public string resultCode { get; set; }
        public string operation_Id { get; set; }
        public string operation_ParentId { get; set; }
        public string duration { get; set; }
        public string performanceBucket { get; set; }
        public string client_IP { get; set; }
        public string client_City { get; set; }
    }
}
