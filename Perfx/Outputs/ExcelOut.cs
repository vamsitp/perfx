namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using ClosedXML.Excel;

    using CsvHelper.Configuration.Attributes;

    public class ExcelOut : IOutput
    {
        private static readonly string[] ExcelColumnNames = new string[] { "A2:A", "B2:B", "C2:C", "D2:D", "E2:E", "F2:F", "G2:G", "H2:H", "I2:I", "J2:J", "L2:L", "M2:M", "N2:N", "O2:O", "P2:P", "Q2:Q", "R2:R", "S2:S", "T2:T", "U2:U", "V2:V", "W2:W", "X2:X", "Y2:Y", "Z2:Z" };

        public Task<bool> Save<T>(IEnumerable<T> results, Settings settings)
        {
            var file = this.GetConnString(settings).GetFullPath();
            var overwrite = settings.QuiteMode;
            if (file.Overwrite(overwrite))
            {
                using (var wb = new XLWorkbook { ReferenceStyle = XLReferenceStyle.Default, CalculateMode = XLCalculateMode.Auto })
                {
                    wb.Style.Font.FontName = "Segoe UI";
                    wb.Style.Font.FontSize = 10;

                    CreateRunsSheet(wb, results);
                    CreateStatsSheet(wb, results);

                    wb.SaveAs(file);
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }

        public Task<IList<T>> Read<T>(Settings settings)
        {
            return this.Read<T>(this.GetConnString(settings), "Perfx_Runs");
        }

        public Task<IList<T>> Read<T>(string file, string sheet)
        {
            var filePath = Path.IsPathRooted(file) ? file : file.GetFullPath();
            if (File.Exists(filePath))
            {
                var results = this.ToList<T>(this.ToDataTable(filePath, sheet));
                return Task.FromResult(results);
            }

            return Task.FromResult(default(IList<T>));
        }

        private IXLWorksheet CreateRunsSheet<T>(XLWorkbook wb, IEnumerable<T> results)
        {
            IXLWorksheet ws = wb.Worksheets.Add("Perfx_Runs");
            var dataTable = this.ToDataTable(results);
            var table = ws.Cell(1, 1).InsertTable(dataTable, "Runs");
            table.Theme = XLTableTheme.TableStyleLight8;

            var rowCount = (dataTable.Rows.Count + 1).ToString();
            var result = ws.Range(ExcelColumnNames[dataTable.Columns["result"].Ordinal] + rowCount);
            result.AddConditionalFormat().WhenNotContains(":").Font.SetFontColor(XLColor.OrangeRed);
            result.AddConditionalFormat().WhenNotContains("200").Font.SetFontColor(XLColor.MediumRedViolet);
            result.AddConditionalFormat().WhenContains("200").Font.SetFontColor(XLColor.SeaGreen);

            var size = ws.Range(ExcelColumnNames[dataTable.Columns["size_b"].Ordinal] + rowCount);
            SetFormat(size, 1000);

            var localms = ws.Range(ExcelColumnNames[dataTable.Columns["local_ms"].Ordinal] + rowCount);
            SetFormat(localms, 1000);

            var aims = ws.Range(ExcelColumnNames[dataTable.Columns["ai_ms"].Ordinal] + rowCount);
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

        private IXLWorksheet CreateStatsSheet<T>(IXLWorkbook wb, IEnumerable<T> results)
        {
            IXLWorksheet wsStats = wb.Worksheets.Add("Perfx_Stats");
            var stats = results.GetStats();

            var statsDataTable = this.ToDataTable(stats.AsEnumerable());
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

        private void SetFormat(IXLRange numbers, int multiplier = 1)
        {
            numbers.AddConditionalFormat().WhenGreaterThan(8 * multiplier).Font.SetFontColor(XLColor.OrangeRed);
            numbers.AddConditionalFormat().WhenGreaterThan(5 * multiplier).Font.SetFontColor(XLColor.MediumRedViolet);
            numbers.AddConditionalFormat().WhenGreaterThan(2 * multiplier).Font.SetFontColor(XLColor.RoyalBlue);
            numbers.AddConditionalFormat().WhenEqualOrLessThan(2 * multiplier).Font.SetFontColor(XLColor.SeaGreen);
        }

        // Credit: https://stackoverflow.com/questions/18100783/how-to-convert-a-list-into-data-table
        public DataTable ToDataTable<T>(IEnumerable<T> items)
        {
            // var properties = TypeDescriptor.GetProperties(typeof(T)).Cast<PropertyDescriptor>().Where(p => !p.Attributes.Cast<Attribute>().Any(a => a.GetType() == typeof(IgnoreAttribute)));
            var properties = typeof(T).GetProperties().Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null);
            var table = new DataTable();
            foreach (var prop in properties)
            {
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }

            foreach (var item in items)
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

        //public DataTable ToDataTable<T>(T item)
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
        public DataTable ToDataTable(string file, dynamic worksheet)
        {
            if (!File.Exists(file))
            {
                return null;
            }

            var dt = new DataTable();
            using (var workBook = new XLWorkbook(file))
            {
                IXLWorksheet workSheet = workBook.Worksheets.Contains(worksheet) ? workBook.Worksheet(worksheet) : workBook.Worksheet(1);
                var firstRow = true;
                foreach (var row in workSheet.RangeUsed().Rows())
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

        private IList<T> ToList<T>(DataTable dt)
        {
            if (dt == null)
            {
                return null;
            }

            var data = new List<T>();
            foreach (var row in dt.Rows.Cast<DataRow>())
            {
                var item = this.ToItem<T>(row);
                data.Add(item);
            }

            return data;
        }

        private T ToItem<T>(DataRow dr)
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
