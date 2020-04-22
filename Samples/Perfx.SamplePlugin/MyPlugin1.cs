namespace Perfx.SamplePlugin
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Perfx;

    public class MyPlugin1 : IPlugin
    {
        public Task<string> GetAuthToken(Settings settings)
        {
            // NOTE: By default Perfx uses IPublicClientApplication's AcquireTokenSilent/AcquireTokenByUsernamePassword/AcquireTokenAsync (see 'Order of Authentication' note in the docs)
            //  If you want to override that behavior and provide a custom implementation, go ahead...
            //  If not, throw NotImplementedException or NotSupportedException, to trigger the default implementation
            var userId = settings.UserId;
            var pwd = settings.Password;

            // Get more settings as required...

            return Task.FromResult("someToken1");
        }

        public Task<List<Endpoint>> GetEndpointDetails(Settings settings)
        {
            //  NOTE: By default Perfx uses Documents/Perfx/Perfx_Inputs.xlsx
            //  If you want to override that behavior and provide a custom implementation, go ahead...
            //  If not, throw NotImplementedException or NotSupportedException, to trigger the default implementation
            var endpointDetails = new List<Endpoint>();
            foreach (var endpoint in settings.Endpoints.Select((e, i) => (url: e, index: i)))
            {
                if (endpoint.url.Contains("odata"))
                {
                    endpointDetails.Add(new Endpoint { Method = HttpMethod.Get.ToString(), Query = "?$top=10" }); // Do whatever - based on the endpoint
                }
                else if (endpoint.url.EndsWith("route1"))
                {
                    endpointDetails.Add(new Endpoint { Method = HttpMethod.Get.ToString(), Query = "/1" }); // Do whatever - based on the endpoint
                }
            }

            return Task.FromResult(endpointDetails);
        }

        public Task<bool> Save<T>(IEnumerable<T> results, Settings settings)
        {
            //  If you want to override that behavior and provide a custom implementation, go ahead...
            //  If not, throw NotImplementedException or NotSupportedException, to trigger the default implementation
            // Do something and return true
            return Task.FromResult(false);
        }

        public Task<IList<T>> Read<T>(Settings settings)
        {
            //  If you want to override that behavior and provide a custom implementation, go ahead...
            //  If not, throw NotImplementedException or NotSupportedException, to trigger the default implementation
            // Do and return something
            return Task.FromResult(default(IList<T>));
        }

        ////public Task<dynamic> ProcessRequest(dynamic record, CancellationToken stopToken = default)
        ////{
        ////    // Get the response whichever way you like from the record details ('record' is of type 'Perfx.Result') and...
        ////    // SET:
        ////    //  record.local_ms // e.g. taskWatch.ElapsedMilliseconds;
        ////    //  record.result // e.g. $"{(int)response.StatusCode}: {response.ReasonPhrase}";
        ////    //  record.size_b // e.g. response.Content.Headers.ContentLength;

        ////    throw new NotImplementedException();
        ////}
    }
}
