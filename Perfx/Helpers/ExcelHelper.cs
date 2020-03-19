namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using ClosedXML.Excel;
    using ColoredConsole;
    using CsvHelper;
    using CsvHelper.Configuration;
    using CsvHelper.Configuration.Attributes;

    public static class ExcelHelper
    {
        private const string ResultsFileName = "Perfx.csv";
        private const string ResultsExcelFileName = "Perfx.xlsx";

        private static readonly string[] ColumnNames = new string[] { "A2:A", "B2:B", "C2:C", "D2:D", "E2:E", "F2:F", "G2:G", "H2:H", "I2:I", "J2:J", "L2:L", "M2:M", "N2:N", "O2:O", "P2:P", "Q2:Q", "R2:R", "S2:S", "T2:T", "U2:U", "V2:V", "W2:W", "X2:X", "Y2:Y", "Z2:Z" };

        public static void SaveToFile<T>(this IEnumerable<T> records, string fileName = ResultsFileName)
        {
            if (fileName.Overwrite())
            {
                using (var reader = File.CreateText(fileName.GetFullPath()))
                {
                    using (var csvWriter = new CsvWriter(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                    {
                        csvWriter.WriteRecords(records);
                    }
                }
            }
        }

        public static List<T> ReadResults<T>(string fileName = ResultsFileName)
        {
            var file = fileName.GetFullPath();
            if (File.Exists(file))
            {
                var textReader = new StreamReader(file);
                using (var csvReader = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    csvReader.Configuration.HeaderValidated = null;
                    csvReader.Configuration.MissingFieldFound = null;
                    var results = csvReader.GetRecords<T>().ToList();
                    return results;
                }
            }

            return null;
        }

        public static void Save<T>(this IEnumerable<T> records, OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Excel)
            {
                SaveToExcel(records);
            }
            else
            {
                SaveToFile(records);
            }
        }

        public static void SaveToExcel<T>(this IEnumerable<T> records, string fileName = ResultsExcelFileName)
        {
            if (fileName.Overwrite())
            {
                using (var wb = new XLWorkbook { ReferenceStyle = XLReferenceStyle.Default, CalculateMode = XLCalculateMode.Auto })
                {
                    wb.Style.Font.FontName = "Segoe UI";
                    wb.Style.Font.FontSize = 10;

                    CreateRunsSheet(wb, records);
                    CreateStatsSheet(wb, records);

                    wb.SaveAs(fileName.GetFullPath());
                }
            }
        }

        private static bool Overwrite(this string fileName)
        {
            var filePath = fileName.GetFullPath();
            if (File.Exists(filePath))
            {
                ColorConsole.Write("\n> ".Red(), "Overwrite ", filePath.DarkYellow(), "?", " (Y/N) ".Red());
                var quit = Console.ReadKey();
                ColorConsole.WriteLine();
                return quit.Key == ConsoleKey.Y;
            }

            return true;
        }

        private static IXLWorksheet CreateRunsSheet<T>(XLWorkbook wb, IEnumerable<T> records)
        {
            IXLWorksheet ws = wb.Worksheets.Add("Perfx_Runs");
            var dataTable = records.ToDataTable();
            var table = ws.Cell(1, 1).InsertTable(dataTable, "Runs");
            table.Theme = XLTableTheme.TableStyleLight8;

            var rowCount = (dataTable.Rows.Count + 1).ToString();
            var result = ws.Range(ColumnNames[dataTable.Columns["result"].Ordinal] + rowCount);
            result.AddConditionalFormat().WhenNotContains(":").Font.SetFontColor(XLColor.OrangeRed);
            result.AddConditionalFormat().WhenNotContains("200").Font.SetFontColor(XLColor.MediumRedViolet);
            result.AddConditionalFormat().WhenContains("200").Font.SetFontColor(XLColor.SeaGreen);

            var size = ws.Range(ColumnNames[dataTable.Columns["size_b"].Ordinal] + rowCount);
            SetFormat(size, 1000);

            var localms = ws.Range(ColumnNames[dataTable.Columns["local_ms"].Ordinal] + rowCount);
            SetFormat(localms, 1000);

            var aims = ws.Range(ColumnNames[dataTable.Columns["ai_ms"].Ordinal] + rowCount);
            SetFormat(aims, 1000);

            try
            {
                ws.SheetView.Freeze(1, 3);
                ws.Columns().AdjustToContents();
            }
            catch (Exception)
            {
                // Expected on Windows Server
            }

            return ws;
        }

        private static IXLWorksheet CreateStatsSheet<T>(IXLWorkbook wb, IEnumerable<T> records)
        {
            IXLWorksheet wsStats = wb.Worksheets.Add("Perfx_Stats");
            var stats = new List<Run>();
            foreach (var group in records.Cast<Record>().GroupBy(x => x.url + (string.IsNullOrWhiteSpace(x.ai_op_Id) ? string.Empty : " (ai)")))
            {
                if (group.Key != null)
                {
                    var run = new Run(group.ToList(), group.Key);
                    stats.Add(run);
                }
            }

            var statsDataTable = stats.AsEnumerable().ToDataTable();
            var statsTable = wsStats.Cell(1, 1).InsertTable(statsDataTable, "Stats");
            statsTable.Theme = XLTableTheme.TableStyleLight8;

            var statsRowCount = (statsDataTable.Rows.Count + 1).ToString();

            var durations = wsStats.Range("B2:I" + statsRowCount);
            SetFormat(durations);

            var sizes = wsStats.Range("J2:K" + statsRowCount);
            SetFormat(sizes, 100);

            wsStats.Range("L2:L" + statsRowCount).Style.Font.SetFontColor(XLColor.SeaGreen);
            wsStats.Range("M2:M" + statsRowCount).Style.Font.SetFontColor(XLColor.OrangeRed);

            try
            {
                wsStats.SheetView.Freeze(1, 1);
                wsStats.Columns().AdjustToContents();
            }
            catch (Exception)
            {
                // Expected on Windows Server
            }

            return wsStats;
        }

        private static void SetFormat(IXLRange numbers, int multiplier = 1)
        {
            numbers.AddConditionalFormat().WhenGreaterThan(8 * multiplier).Font.SetFontColor(XLColor.OrangeRed);
            numbers.AddConditionalFormat().WhenGreaterThan(5 * multiplier).Font.SetFontColor(XLColor.MediumRedViolet);
            numbers.AddConditionalFormat().WhenGreaterThan(2 * multiplier).Font.SetFontColor(XLColor.RoyalBlue);
            numbers.AddConditionalFormat().WhenEqualOrLessThan(2 * multiplier).Font.SetFontColor(XLColor.SeaGreen);
        }

        public static List<T> Read<T>(this OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Excel)
            {
                return ReadFromExcel<T>() ?? ReadResults<T>();
            }
            else
            {
                return ReadResults<T>();
            }
        }

        public static List<T> ReadFromExcel<T>(string filename = ResultsExcelFileName, string sheet = "Perfx_Runs")
        {
            var records = filename.ToDataTable(sheet)?.ToList<T>();
            return records;
        }

        // Credit: https://stackoverflow.com/questions/18100783/how-to-convert-a-list-into-data-table
        public static DataTable ToDataTable<T>(this IEnumerable<T> data)
        {
            // var properties = TypeDescriptor.GetProperties(typeof(T)).Cast<PropertyDescriptor>().Where(p => !p.Attributes.Cast<Attribute>().Any(a => a.GetType() == typeof(IgnoreAttribute)));
            var properties = typeof(T).GetProperties().Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null);
            var table = new DataTable();
            foreach (var prop in properties)
            {
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }

            foreach (var item in data)
            {
                var row = table.NewRow();
                foreach (var prop in properties)
                {
                    if (prop.PropertyType == typeof(DateTime) && (DateTime)prop.GetValue(item) == DateTime.MinValue)
                    {
                        row[prop.Name] = new DateTime(1900, 01, 01);
                    }
                    else
                    {
                        row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                    }
                }

                table.Rows.Add(row);
            }

            return table;
        }

        //public static DataTable ToDataTable<T>(this T item)
        //{
        //    var properties = typeof(T).GetProperties().Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null);
        //    var table = new DataTable();
        //    foreach (var prop in properties)
        //    {
        //        table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
        //    }

        //    var row = table.NewRow();
        //    foreach (var prop in properties)
        //    {
        //        if (prop.PropertyType == typeof(DateTime) && (DateTime)prop.GetValue(item) == DateTime.MinValue)
        //        {
        //            row[prop.Name] = new DateTime(1900, 01, 01);
        //        }
        //        else
        //        {
        //            row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
        //        }
        //    }

        //    table.Rows.Add(row);
        //    return table;
        //}

        // Credit: https://stackoverflow.com/a/53546001
        public static DataTable ToDataTable(this string fileName, dynamic worksheet)
        {
            var filePath = fileName.GetFullPath();
            if (!File.Exists(filePath))
            {
                return null;
            }

            var dt = new DataTable();
            using (var workBook = new XLWorkbook(fileName.GetFullPath()))
            {
                IXLWorksheet workSheet = workBook.Worksheets.Contains(worksheet) ? workBook.Worksheet(worksheet) : workBook.Worksheet(1);
                var firstRow = true;
                foreach (var row in workSheet.Rows())
                {
                    if (firstRow)
                    {
                        foreach (var cell in row.Cells())
                        {
                            if (!string.IsNullOrEmpty(cell.Value.ToString()))
                            {
                                dt.Columns.Add(cell.Value.ToString());
                            }
                            else
                            {
                                break;
                            }
                        }

                        firstRow = false;
                    }
                    else
                    {
                        var i = 0;
                        var toInsert = dt.NewRow();
                        foreach (var cell in row.Cells(1, dt.Columns.Count))
                        {
                            try
                            {
                                toInsert[i] = cell.Value.ToString();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex);
                            }

                            i++;
                        }
                        dt.Rows.Add(toInsert);
                    }
                }

                return dt;
            }
        }

        private static List<T> ToList<T>(this DataTable dt)
        {
            if (dt == null)
            {
                return null;
            }

            var data = new List<T>();
            foreach (var row in dt.Rows.Cast<DataRow>())
            {
                var item = row.ToItem<T>();
                data.Add(item);
            }

            return data;
        }

        private static T ToItem<T>(this DataRow dr)
        {
            var properties = typeof(T).GetProperties().Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null);
            var instance = Activator.CreateInstance<T>();
            foreach (var column in dr.Table.Columns.Cast<DataColumn>())
            {
                foreach (var prop in properties)
                {
                    if (prop.Name.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase) && prop.SetMethod != null)
                    {
                        var value = dr[column.ColumnName];
                        var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        prop.SetValue(instance, Convert.ChangeType(value is string && string.IsNullOrEmpty(value?.ToString()) ? (propType.IsValueType ? Activator.CreateInstance(propType) : null) : value, propType), null);
                    }
                }
            }

            return instance;
        }
    }
}
