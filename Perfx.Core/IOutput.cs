namespace Perfx
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IOutput
    {
        Task<IList<T>> Read<T>(Settings settings);

        Task<bool> Save<T>(IEnumerable<T> results, Settings settings);
    }
}
