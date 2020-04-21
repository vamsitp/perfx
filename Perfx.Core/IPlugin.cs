namespace Perfx
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPlugin : IOutput
    {
        // TODO: Use SecurePassword?
        Task<string> GetAuthToken(Settings settings);

        Task<List<Endpoint>> GetEndpointDetails(Settings settings);

        //// Task<dynamic> ProcessRequest(dynamic record, CancellationToken stopToken = default);
    }
}
