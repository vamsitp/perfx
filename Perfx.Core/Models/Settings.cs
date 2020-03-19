namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using Newtonsoft.Json;

    public class Settings
    {
        [JsonIgnore]
        public readonly static string AppSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nameof(Perfx),  $"{nameof(Perfx)}.json");

        [JsonIgnore]
        public readonly static string DefaultSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nameof(Perfx), $"{nameof(Perfx)}.Defaults.json");

        private PropertyInfo[] properties;

        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Authority { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public IEnumerable<string> ApiScopes { get; set; }
        public string AppInsightsAppId { get; set; } = string.Empty;
        public string AppInsightsApiKey { get; set; } = string.Empty;
        public IEnumerable<string> Endpoints { get; set; }
        public int Iterations { get; set; } = 5;
        public string InputsFile { get; set; } = "Perfx_Inputs.xlsx";
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Excel;
        public bool ReadResponseHeadersOnly { get; set; } = false;
        public string PluginClassName { get; set; }

        [JsonIgnore]
        public string Token { get; set; }

        [JsonIgnore]
        public PropertyInfo[] Properties
        {
            get
            {
                if (properties == null)
                {
                    properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                }

                return properties;
            }
            set
            {
                properties = value;
            }
        }

        public void Save()
        {
            File.WriteAllText(AppSettingsFile, JsonConvert.SerializeObject(this, Formatting.Indented));
            // (this as IConfigurationRoot).Reload();
        }
    }

    public enum OutputFormat
    {
        Excel,
        Csv
    }
}
