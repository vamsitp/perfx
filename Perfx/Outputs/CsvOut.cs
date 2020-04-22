namespace Perfx
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using CsvHelper;
    using CsvHelper.Configuration;

    public class CsvOut : IOutput
    {
        public Task<bool> Save<T>(IEnumerable<T> results, Settings settings)
        {
            var file = settings.OutputFile.GetFullPath();
            var overwrite = settings.QuiteMode;
            if (file.Overwrite(overwrite))
            {
                using (var reader = File.CreateText(file))
                {
                    using (var csvWriter = new CsvWriter(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                    {
                        csvWriter.WriteRecords(results);
                    }
                }

                var stats = results.GetStats<T>(file);
                using (var reader = File.CreateText(stats.statsFile))
                {
                    using (var csvWriter = new CsvWriter(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                    {
                        csvWriter.WriteRecords(stats.stats);
                    }
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<IList<T>> Read<T>(Settings settings)
        {
            var file = settings.OutputFile.GetFullPath();
            if (File.Exists(file))
            {
                var textReader = new StreamReader(file);
                using (var csvReader = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    csvReader.Configuration.HeaderValidated = null;
                    csvReader.Configuration.MissingFieldFound = null;
                    IList<T> results = csvReader.GetRecords<T>().ToList();
                    return Task.FromResult(results);
                }
            }

            return Task.FromResult(default(IList<T>));
        }
    }
}
