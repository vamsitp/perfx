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
        private string tenant;

        [JsonIgnore]
        private static Dictionary<OutputFormat, string> OutputExtensions = new Dictionary<OutputFormat, string>
        {
            { OutputFormat.Excel, "_Results.xlsx" },
            { OutputFormat.Csv, "_Results.csv" },
            { OutputFormat.Json, "_Results.json" }
        };

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
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Excel;
        public bool ReadResponseHeadersOnly { get; set; } = false;
        public string PluginClassName { get; set; }
        public bool QuiteMode { get; set; }

        [JsonIgnore]
        public string Authority => $"https://login.microsoftonline.com/{this.Tenant}";

        [JsonIgnore]
        public string OutputFile => Path.GetFileNameWithoutExtension(this.AppSettingsFile).Replace(".Settings", string.Empty) + OutputExtensions[this.OutputFormat];

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
    }

    public enum OutputFormat
    {
        Excel,
        Csv,
        Json
    }
}
