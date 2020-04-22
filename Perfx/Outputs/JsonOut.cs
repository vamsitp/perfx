namespace Perfx
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    public class JsonOut : IOutput
    {
        public Task<bool> Save<T>(IEnumerable<T> results, Settings settings)
        {
            var file = settings.OutputFile.GetFullPath();
            var overwrite = settings.QuiteMode;
            if (file.Overwrite(overwrite))
            {
                File.WriteAllText(file, JsonConvert.SerializeObject(results, Formatting.Indented, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
                var stats = results.GetStats<T>(file);
                File.WriteAllText(stats.statsFile, JsonConvert.SerializeObject(stats.stats, Formatting.Indented, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<IList<T>> Read<T>(Settings settings)
        {
            var file = settings.OutputFile.GetFullPath();
            if (File.Exists(file))
            {
                return Task.FromResult(JsonConvert.DeserializeObject<IList<T>>(File.ReadAllText(file)));
            }

            return Task.FromResult(default(IList<T>));
        }
    }
}
