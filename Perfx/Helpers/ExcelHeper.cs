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

    using CsvHelper;
    using CsvHelper.Configuration;
    using CsvHelper.Configuration.Attributes;

    public static class ExcelHeper
    {
        private const string ResultsFileName = "Perfx.csv";
        private const string ResultsExcelFileName = "Perfx.xlsx";

        private static readonly string[] ColumnNames = new string[] { "A2:A", "B2:B", "C2:C", "D2:D", "E2:E", "F2:F", "G2:G", "H2:H", "I2:I", "J2:J", "L2:L", "M2:M", "N2:N", "O2:O", "P2:P", "Q2:Q", "R2:R", "S2:S", "T2:T", "U2:U", "V2:V", "W2:W", "X2:X", "Y2:Y", "Z2:Z" };

        public static void SaveToFile<T>(this IEnumerable<T> records, string fileName = ResultsFileName)
        {
            var file = fileName.GetFullPath();
            using (var reader = File.CreateText(file))
            {
                using (var csvWriter = new CsvWriter(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    csvWriter.WriteRecords(records);
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
            using (var wb = new XLWorkbook { ReferenceStyle = XLReferenceStyle.Default, CalculateMode = XLCalculateMode.Auto })
            {
                wb.Style.Font.FontName = "Segoe UI";
                wb.Style.Font.FontSize = 10;

                IXLWorksheet ws = wb.Worksheets.Add("Perfx_Runs");
                var dataTable = records.ToDataTable();
                var table = ws.Cell(1, 1).InsertTable(dataTable, "Runs");
                table.Theme = XLTableTheme.TableStyleLight8;

                var rowCount = (dataTable.Rows.Count + 1).ToString();
                var result = ws.Range(ColumnNames[dataTable.Columns["result"].Ordinal] + rowCount);
                result.AddConditionalFormat().WhenNotContains(":").Font.SetFontColor(XLColor.OrangeRed);
                result.AddConditionalFormat().WhenNotContains("200").Font.SetFontColor(XLColor.MediumRedViolet);
                result.AddConditionalFormat().WhenContains("200").Font.SetFontColor(XLColor.SeaGreen);

                var size = ws.Range(ColumnNames[dataTable.Columns["size"].Ordinal] + rowCount);
                size.AddConditionalFormat().WhenGreaterThan(8000).Font.SetFontColor(XLColor.OrangeRed);
                size.AddConditionalFormat().WhenGreaterThan(5000).Font.SetFontColor(XLColor.MediumRedViolet);
                size.AddConditionalFormat().WhenGreaterThan(2000).Font.SetFontColor(XLColor.RoyalBlue);
                size.AddConditionalFormat().WhenEqualOrLessThan(2000).Font.SetFontColor(XLColor.SeaGreen);

                var localms = ws.Range(ColumnNames[dataTable.Columns["local_ms"].Ordinal] + rowCount);
                localms.AddConditionalFormat().WhenGreaterThan(8000).Font.SetFontColor(XLColor.OrangeRed);
                localms.AddConditionalFormat().WhenGreaterThan(5000).Font.SetFontColor(XLColor.MediumRedViolet);
                localms.AddConditionalFormat().WhenGreaterThan(2000).Font.SetFontColor(XLColor.RoyalBlue);
                localms.AddConditionalFormat().WhenEqualOrLessThan(2000).Font.SetFontColor(XLColor.SeaGreen);

                ws.Columns().AdjustToContents();
                ws.SheetView.Freeze(1, 3);
                wb.SaveAs(fileName.GetFullPath());
            }
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

        public static List<T> ReadFromExcel<T>(string filename = ResultsExcelFileName)
        {
            var records = filename.GetDataTable("Perfx_Runs")?.GetList<T>();
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

        // Credit: https://stackoverflow.com/a/53546001
        public static DataTable GetDataTable(this string fileName, dynamic worksheet)
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

        private static List<T> GetList<T>(this DataTable dt)
        {
            if (dt == null)
            {
                return null;
            }

            var data = new List<T>();
            foreach (var row in dt.Rows.Cast<DataRow>())
            {
                var item = row.GetItem<T>();
                data.Add(item);
            }

            return data;
        }

        private static T GetItem<T>(this DataRow dr)
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
