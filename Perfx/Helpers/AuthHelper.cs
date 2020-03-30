namespace Perfx
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Security;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Flurl.Http;

    using Microsoft.Identity.Client;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    using Newtonsoft.Json.Linq;

    using AuthenticationResult = Microsoft.Identity.Client.AuthenticationResult;

    class AuthHelper
    {
        internal const string ClientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";    // Change to your app registration's Application ID, unless you are an MSA backed account
        internal const string ReplyUrl = "urn:ietf:wg:oauth:2.0:oob";               // Change to your app registration's reply URI, unless you are an MSA backed account
        internal const string ResourceUrl = "https://graph.windows.net";            // https://graph.microsoft.com
        internal const string TenantInfoUrl = "https://login.windows.net/{0}.onmicrosoft.com/.well-known/openid-configuration";

        internal static readonly ConcurrentDictionary<string, Lazy<Task<string>>> AuthTokens = new ConcurrentDictionary<string, Lazy<Task<string>>>();

        public static async Task<string> GetAuthTokenByUserCredentialsSilentAsync(Settings input)
        {
            ColorConsole.WriteLine("Acquiring token for ", input.UserId.Green(), " ...");
            var app = PublicClientApplicationBuilder.Create(input.ClientId).WithAuthority(input.Authority).Build();
            var accounts = await app.GetAccountsAsync().ConfigureAwait(continueOnCapturedContext: false);
            AuthenticationResult result = null;
            if (accounts?.Any() ?? false)
            {
                result = await app.AcquireTokenSilent(input.ApiScopes, accounts.FirstOrDefault()).ExecuteAsync();
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
                    result = await app.AcquireTokenByUsernamePassword(input.ApiScopes, input.UserId, securePassword).ExecuteAsync();
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

        ////public static async Task<string> GetAuthTokenInteractiveAsync(Settings input)
        ////{
        ////    var accessToken = await AuthTokens.GetOrAdd(input.TenantId ?? string.Empty, k =>
        ////    {
        ////        return new Lazy<Task<string>>(async () =>
        ////        {
        ////            var ctx = GetAuthenticationContext(input.TenantId);
        ////            Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationResult result = null;
        ////            var promptBehavior = new PlatformParameters(PromptBehavior.SelectAccount, new CustomWebUi());
        ////            ColorConsole.Write("Authenticating...\n");
        ////            try
        ////            {
        ////                result = await ctx.AcquireTokenAsync(ResourceUrl, input.ClientId ?? ClientId, new Uri(input.ReplyUrl ?? ReplyUrl), promptBehavior);
        ////            }
        ////            catch (UnauthorizedAccessException)
        ////            {
        ////                // If the token has expired, prompt the user with a login prompt
        ////                result = await ctx.AcquireTokenAsync(ResourceUrl, input.ClientId ?? ClientId, new Uri(input.ReplyUrl ?? ReplyUrl), promptBehavior);
        ////            }

        ////            return result?.AccessToken;
        ////        });
        ////    }).Value;

        ////    return accessToken;
        ////}

        ////// https://docs.microsoft.com/en-us/power-bi/developer/automation/walkthrough-push-data-get-token
        ////// https://blog.jpries.com/2020/01/03/getting-started-with-the-power-bi-api-querying-the-power-bi-rest-api-directly-with-c/
        ////public static async Task<string> GetAuthTokenByClientCredentialsAsync(Settings input)
        ////{
        ////    AuthenticationContext authContext = new AuthenticationContext(input.Authority);
        ////    var token = await authContext.AcquireTokenAsync(input.ResourceUrl, new Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential(input.ClientId, input.ClientSecret));
        ////    return token.AccessToken;
        ////}

        ////// Credit: https://stackoverflow.com/a/39590155
        ////public static async Task<string> GetAuthTokenByUserCredentialsRawAsync(Settings input)
        ////{
        ////    var client = new HttpClient();
        ////    string tokenEndpoint = $"https://login.microsoftonline.com/{input.TenantId}/oauth2/token";
        ////    var body = $"resource={input.ResourceUrl}&client_id={input.ClientId}&client_secret={input.ClientSecret}&grant_type=password&username={input.UserId}&password={input.Password}";
        ////    var stringContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        ////    var result = await client.PostAsync(tokenEndpoint, stringContent).ContinueWith(response =>
        ////    {
        ////        return response.Result.Content.ReadAsStringAsync().Result;
        ////    });

        ////    var jobject = JObject.Parse(result);
        ////    var token = jobject["access_token"].Value<string>();
        ////    return token;
        ////}

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
