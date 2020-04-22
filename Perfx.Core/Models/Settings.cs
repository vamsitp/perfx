namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Newtonsoft.Json;

    public class Settings
    {
        private const string OnMicrosoft = ".onmicrosoft.com";
        private static Dictionary<string, string> OutputExtensions = new Dictionary<string, string>
        {
            { Perfx.OutputFormat.Excel.ToString(), "_Results.xlsx" },
            { Perfx.OutputFormat.Csv.ToString(), "_Results.csv" },
            { Perfx.OutputFormat.Json.ToString(), "_Results.json" }
        };

        private string tenant;
        public string Tenant
        {
            get
            {
                return this.tenant;
            }
            set
            {
                if (Guid.TryParse(value, out var tenantId))
                {
                    this.tenant = value;
                }
                else
                {
                    this.tenant = string.IsNullOrWhiteSpace(value) ? value : (value.ToLowerInvariant().Replace(OnMicrosoft, string.Empty) + OnMicrosoft);
                }
            }
        }

        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string ResourceUrl { get; set; } = string.Empty;
        public string ReplyUrl { get; set; } = string.Empty;
        public IEnumerable<string> ApiScopes { get; set; }
        public string AppInsightsAppId { get; set; } = string.Empty;
        public string AppInsightsApiKey { get; set; } = string.Empty;
        public IEnumerable<string> Endpoints { get; set; }
        public int Iterations { get; set; } = 5;
        public string InputsFile { get; set; } = "Perfx_Inputs.xlsx";
        public string OutputFormat { get; set; } // for backward-compatibility; to be removed later!
        public string[] OutputFormats { get; set; } // = new[] { Perfx.OutputFormat.Excel.ToString() };
        public bool ReadResponseHeadersOnly { get; set; } = false;
        public string PluginClassName { get; set; }
        public bool QuiteMode { get; set; }
        public double ResponseTimeSla { get; set; } = 5;
        public double ResponseSizeSla { get; set; } = 200;

        [JsonIgnore]
        public List<Output> Outputs => this.GetOutputs();

        [JsonIgnore]
        public string Authority => $"https://login.microsoftonline.com/{this.Tenant}";

        [JsonIgnore]
        public ExpandoObject FormatArgs { get; set; }

        [JsonIgnore]
        public string Token { get; set; }

        [JsonIgnore]
        public List<PropertyInfo> Properties { get; } = typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null).ToList();

        [JsonIgnore]
        public string AppSettingsFile { get; set; } = DefaultAppSettingsFile;

        [JsonIgnore]
        public readonly static string DefaultAppSettingsFile = $"{nameof(Perfx)}.Settings.json".GetFullPath();

        [JsonIgnore]
        public readonly static string DefaultLogSettingsFile = $"{nameof(Perfx)}.Logging.json".GetFullPath();

        public void Save()
        {
            File.WriteAllText(this.AppSettingsFile, JsonConvert.SerializeObject(this, Formatting.Indented));
            // (this as IConfigurationRoot).Reload();
        }

        private List<Output> GetOutputs()
        {
            var outputFormats = this.OutputFormats?.Length > 0 ? this.OutputFormats : new[] { this.OutputFormat }; // backward-compatibility
            var outputs = outputFormats?.Select(x => x.Split(new[] { "::" }, 2, StringSplitOptions.None));
            return outputs?.Select(o =>
            {
                var output = new Output { Format = (OutputFormat)Enum.Parse(typeof(OutputFormat), o.FirstOrDefault().Trim()) };
                if (o.Length > 1)
                {
                    output.ConnString = o.LastOrDefault().Trim();
                }
                else
                {
                    output.ConnString = Path.GetFileNameWithoutExtension(this.AppSettingsFile).Replace(".Settings", string.Empty) + OutputExtensions[o.FirstOrDefault().Trim()];
                }

                return output;
            })?.ToList();
        }
    }

    public enum OutputFormat
    {
        Excel,
        Csv,
        Json,
        Sql
    }

    public class Output
    {
        public OutputFormat Format { get; set; }
        public string ConnString { get; set; }
    }
}
