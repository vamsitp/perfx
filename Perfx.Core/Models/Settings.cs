namespace Perfx
{
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Newtonsoft.Json;

    public class Settings
    {
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Authority { get; set; } = string.Empty; // => $"https://login.microsoftonline.com/{this.TenantName.ToLowerInvariant().Replace(".onmicrosoft.com", string.Empty)}.onmicrosoft.com";
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
        public ExpandoObject FormatArgs { get; set; }

        [JsonIgnore]
        public string Token { get; set; }

        [JsonIgnore]
        public List<PropertyInfo> Properties { get; } = typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null).ToList();

        [JsonIgnore]
        public readonly static string AppSettingsFile = $"{nameof(Perfx)}.Settings.json".GetFullPath();

        [JsonIgnore]
        public readonly static string DefaultSettingsFile = $"{nameof(Perfx)}.Logging.json".GetFullPath();

        public void Save()
        {
            File.WriteAllText(AppSettingsFile, JsonConvert.SerializeObject(this, Formatting.Indented));
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
