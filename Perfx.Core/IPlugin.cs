namespace Perfx
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPlugin
    {
        // TODO: Use SecurePassword?
        Task<string> GetAuthToken(Settings settings);

        Task<List<RunInput>> GetInputs();

        Task<RunInput> GetInput(string endpoint, int index);
    }
}
