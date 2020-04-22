namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Dapper;

    using FastMember;

    public class SqlOut : IOutput
    {
        private const string ResultsTable = "[dbo].[Perfx_Results]";
        private const string StatsTable = "[dbo].[Perfx_Stats]";

        private static readonly string ResultsSql = $"SELECT TOP 100 r.* FROM {ResultsTable} AS r INNER JOIN {StatsTable} as s ON r.run_Id = s.id AND s.run_by = '{Extensions.UserName}' ORDER BY r.timestamp DESC";
        private static readonly string StatsSql = $"SELECT TOP {{0}} id, url FROM {StatsTable} WHERE run_by = '{Extensions.UserName}' ORDER BY 'id' DESC";

        private static readonly string[] AddStatsCols = new[] { "url", "run_by" };
        private static readonly string[] AddResultsCols = new[] { "run_Id" };

        public async Task<bool> Save<T>(IEnumerable<T> results, Settings settings)
        {
            // TODO: Handle this properly using Dapper?
            if (results?.Any() == true)
            {
                try
                {
                    var connString = this.GetConnString(settings);

                    // Bulk-insert Stats
                    var stats = results.GetStats<T>();
                    var statsCopyParams = new List<string>();
                    statsCopyParams.AddRange(AddStatsCols);
                    statsCopyParams.AddRange(stats.FirstOrDefault().Properties.Select(p => p.Name));
                    using var sqlCopyStats = new SqlBulkCopy(connString) { DestinationTableName = StatsTable, BatchSize = 1000 };
                    statsCopyParams.ForEach(p => sqlCopyStats.ColumnMappings.Add(p, p));
                    using var statsReader = ObjectReader.Create(stats);
                    await sqlCopyStats.WriteToServerAsync(statsReader);

                    // Get the inserted-Ids
                    using var conn = new SqlConnection(connString);
                    var statIds = await conn.QueryAsync<dynamic>(string.Format(StatsSql, stats.Count));

                    // Map the run-Ids to results
                    var resultsList = results.Cast<Result>().ToList();
                    resultsList.ForEach(r => r.run_Id = statIds.SingleOrDefault(s => r.url.StartsWith(s.url)).id);

                    // Bulk-insert Results
                    var resultsCopyParams = new List<string>();
                    resultsCopyParams.AddRange(AddResultsCols);
                    resultsCopyParams.AddRange((resultsList.FirstOrDefault()).Properties.Select(p => p.Name));
                    using var sqlCopyResults = new SqlBulkCopy(connString) { DestinationTableName = ResultsTable, BatchSize = 1000 };
                    resultsCopyParams.ForEach(p => sqlCopyResults.ColumnMappings.Add(p, p));
                    using var resultsReader = ObjectReader.Create(resultsList);
                    await sqlCopyResults.WriteToServerAsync(resultsReader);

                    return true;
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.White().OnRed());
                }
            }

            return false;
        }

        public async Task<IList<T>> Read<T>(Settings settings)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(this.GetConnString(settings));
                using var conn = new SqlConnection(builder.ConnectionString);
                var results = await conn.QueryAsync<T>(ResultsSql);
                return results.ToList(); // TODO: Populate details property
            }
            catch (SqlException ex)
            {
                ColorConsole.WriteLine(ex.Message.White().OnRed());
            }

            return default(IList<T>);
        }
    }
}
