namespace Perfx.SamplePlugin
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class MyPlugin1 : IPlugin
    {
        public Task<string> GetAuthToken(Settings settings)
        {
            var userId = settings.UserId;
            var pwd = settings.Password;

            // Get more settings as required...

            return Task.FromResult("someToken1");
        }

        public Task<List<Endpoint>> GetEndpointDetails(Settings settings)
        {
            throw new NotImplementedException();
        }
    }
}
