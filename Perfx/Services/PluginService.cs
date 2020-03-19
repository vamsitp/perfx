namespace Perfx
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class PluginService : IPlugin
    {
        public Task<string> GetAuthToken(Settings settings)
        {
            // return await AuthHelper.GetAuthToken(tenant);
            return AuthHelper.GetAuthTokenSilentAsync(settings);
        }

        public Task<List<Endpoint>> GetEndpointDetails(Settings settings)
        {
            return Task.FromResult(ExcelHelper.ReadFromExcel<Endpoint>(settings.InputsFile, "Inputs"));
        }
    }
}
