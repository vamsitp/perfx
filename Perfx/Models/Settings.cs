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
        public readonly static string AppSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{nameof(Perfx)}.json");
        [JsonIgnore]
        public readonly static string DefaultSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{nameof(Perfx)}.Defaults.json");

        private PropertyInfo[] properties;

        public string UserId { get; set; }
        public string Password { get; set; }
        public string Authority { get; set; }
        public string ClientId { get; set; }
        public IEnumerable<string> ApiScopes { get; set; }
        public IEnumerable<string> Endpoints { get; set; }
        public int Iterations { get; set; } = 5;
        public string AppInsightsAppId { get; set; }
        public string AppInsightsApiKey { get; set; }
        //public object Logging { get; set; }

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
        }
    }

    public class InvalidAuthTokenError
    {
        public Error error { get; set; }
    }

    public class Error
    {
        public string code { get; set; }
        public string message { get; set; }
    }
}
