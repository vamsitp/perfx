namespace Perfx
{
    using System.Collections.Generic;
    using System.Reflection;
    using Newtonsoft.Json;

    public class Input
    {
        private PropertyInfo[] properties;

        public string Token { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string Authority { get; set; }
        public string ClientId { get; set; }
        public IEnumerable<string> ApiScopes { get; set; }

        public IEnumerable<string> Endpoints { get; set; }

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
