namespace Perfx
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Security;
    using System.Text;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Flurl.Http;

    using Microsoft.Identity.Client;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    using Newtonsoft.Json.Linq;

    using AuthenticationResult = Microsoft.Identity.Client.AuthenticationResult;

    class AuthHelper
    {
        internal const string TenantInfoUrl = "https://login.windows.net/{0}.onmicrosoft.com/.well-known/openid-configuration";

        internal static readonly ConcurrentDictionary<string, Lazy<Task<string>>> AuthTokens = new ConcurrentDictionary<string, Lazy<Task<string>>>();

        public static async Task<string> GetAuthTokenByUserCredentialsSilentAsync(Settings input)
        {
            var apiScopes = GetApiScopes(input);
            if (string.IsNullOrWhiteSpace(input.Tenant) || string.IsNullOrWhiteSpace(input.ClientId) || string.IsNullOrWhiteSpace(input.UserId) || string.IsNullOrWhiteSpace(input.Password) || apiScopes?.Any() != true)
            {
                throw new ArgumentException($"To use the User-credentials silent-flow, please provide valid Tenant, ClientId, UserId, Password, ApiScopes in '{input.AppSettingsFile}'");
            }

            ColorConsole.WriteLine("Acquiring token for ", input.UserId.Green(), " ...");
            var app = PublicClientApplicationBuilder.Create(input.ClientId).WithAuthority(input.Authority).Build();
            var accounts = await app.GetAccountsAsync().ConfigureAwait(continueOnCapturedContext: false);
            AuthenticationResult result = null;
            if (accounts?.Any() == true)
            {
                result = await app.AcquireTokenSilent(apiScopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            else
            {
                try
                {
                    var securePassword = new SecureString();
                    var pwd = input.Password;
                    foreach (char c in pwd)
                    {
                        securePassword.AppendChar(c);
                    }
                    result = await app.AcquireTokenByUsernamePassword(apiScopes, input.UserId, securePassword).ExecuteAsync();
                }
                catch (MsalException mex)
                {
                    ColorConsole.WriteLine(mex.Message.White().OnRed());
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.White().OnRed());
                }
            }

            return result.AccessToken;
        }

        public static async Task<string> GetAuthTokenByUserCredentialsInteractiveAsync(Settings input)
        {
            var resource = GetResourceUrl(input);
            if (string.IsNullOrWhiteSpace(input.Tenant) || string.IsNullOrWhiteSpace(input.ClientId) || string.IsNullOrWhiteSpace(resource) || string.IsNullOrWhiteSpace(input.ReplyUrl))
            {
                throw new ArgumentException($"To use the User-credentials interactive-flow, please provide valid Tenant, ClientId, ResourceUrl, ReplyUrl in '{input.AppSettingsFile}'");
            }

            var accessToken = await AuthTokens.GetOrAdd(input.Tenant ?? string.Empty, k =>
            {
                return new Lazy<Task<string>>(async () =>
                {
                    var ctx = GetAuthenticationContext(input.Tenant);
                    Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationResult result = null;
                    var promptBehavior = new PlatformParameters(PromptBehavior.SelectAccount, new CustomWebUi());
                    ColorConsole.Write("Authenticating...\n");
                    try
                    {
                        result = await ctx.AcquireTokenAsync(resource, input.ClientId, new Uri(input.ReplyUrl), promptBehavior);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // If the token has expired, prompt the user with a login prompt
                        result = await ctx.AcquireTokenAsync(resource, input.ClientId, new Uri(input.ReplyUrl), promptBehavior);
                    }

                    return result?.AccessToken;
                });
            }).Value;

            return accessToken;
        }

        // https://docs.microsoft.com/en-us/power-bi/developer/automation/walkthrough-push-data-get-token
        // https://blog.jpries.com/2020/01/03/getting-started-with-the-power-bi-api-querying-the-power-bi-rest-api-directly-with-c/
        public static async Task<string> GetAuthTokenByClientCredentialsAsync(Settings input)
        {
            var resource = GetResourceUrl(input);
            if (string.IsNullOrWhiteSpace(input.Tenant) || string.IsNullOrWhiteSpace(input.ClientId) || string.IsNullOrWhiteSpace(input.ClientSecret) || string.IsNullOrWhiteSpace(resource))
            {
                throw new ArgumentException($"To use the Client-credentials flow, please provide valid Tenant, ClientId, ClientSecret, ResourceUrl in '{input.AppSettingsFile}'");
            }

            AuthenticationContext authContext = new AuthenticationContext(input.Authority);
            var token = await authContext.AcquireTokenAsync(resource, new Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential(input.ClientId, input.ClientSecret));
            return token.AccessToken;
        }

        // Credit: https://stackoverflow.com/a/39590155
        public static async Task<string> GetAuthTokenByUserCredentialsRawAsync(Settings input)
        {
            var resource = GetResourceUrl(input);
            if (string.IsNullOrWhiteSpace(input.Tenant) || string.IsNullOrWhiteSpace(input.ClientId) || string.IsNullOrWhiteSpace(input.ClientSecret) || string.IsNullOrWhiteSpace(resource))
            {
                throw new ArgumentException($"To use the User-credentials raw-flow, please provide valid Tenant, ClientId, ClientSecret, UserId, Password, ResourceUrl in '{input.AppSettingsFile}'");
            }

            var client = new HttpClient();
            string tokenEndpoint = $"https://login.microsoftonline.com/{input.Tenant}/oauth2/token";
            var body = $"resource={resource}&client_id={input.ClientId}&client_secret={input.ClientSecret}&grant_type=password&username={input.UserId}&password={input.Password}";
            var stringContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            var result = await client.PostAsync(tokenEndpoint, stringContent).ContinueWith(response =>
            {
                return response.Result.Content.ReadAsStringAsync().Result;
            });

            var jobject = JObject.Parse(result);
            var token = jobject["access_token"].Value<string>();
            return token;
        }

        public static async Task<string> GetTenantId(string tenant)
        {
            if (!Guid.TryParse(tenant, out var tenantId))
            {
                var url = string.Format(TenantInfoUrl, tenant.ToLowerInvariant().Replace(".onmicrosoft.com", string.Empty));
                var json = await url.GetJsonAsync<JObject>();
                return json?.SelectToken(".issuer")?.Value<string>()?.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)?.LastOrDefault();
            }

            return tenant;
        }

        private static string GetResourceUrl(Settings input)
        {
            return string.IsNullOrWhiteSpace(input.ResourceUrl) ? input.ClientId : input.ResourceUrl;
        }

        private static IEnumerable<string> GetApiScopes(Settings input)
        {
            return input.ApiScopes?.Any() == true ? input.ApiScopes : new string[] { $"api://{input.ClientId}/.default" };
        }

        private static AuthenticationContext GetAuthenticationContext(string tenant)
        {
            AuthenticationContext ctx = null;
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                ctx = new AuthenticationContext("https://login.microsoftonline.com/" + tenant);
            }
            else
            {
                ctx = new AuthenticationContext("https://login.windows.net/common");
                if (ctx.TokenCache.Count > 0)
                {
                    string homeTenant = ctx.TokenCache.ReadItems().First().TenantId;
                    ctx = new AuthenticationContext("https://login.microsoftonline.com/" + homeTenant);
                }
            }

            return ctx;
        }
    }
}
