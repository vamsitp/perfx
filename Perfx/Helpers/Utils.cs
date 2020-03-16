namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Alba.CsConsoleFormat;

    using ColoredConsole;

    using CsvHelper;
    using CsvHelper.Configuration;

    using MathNet.Numerics.Statistics;

    public static class Utils
    {
        private const string ResultsFileName = "Perfx.csv";
        private const int MaxBarLength = 100;
        private const string VerticalChar = "│"; // "┃"
        private const char HorizontalChar = '─'; // '━'
        private const string BroderChar = "└"; // "┗"

        // Credits: https://devblogs.microsoft.com/pfxteam/processing-tasks-as-they-complete, https://stackoverflow.com/a/58194681
        public static Task<Task<T>>[] Interleaved<T>(this IEnumerable<Task<T>> tasks)
        {
            var inputTasks = tasks.ToList();

            var buckets = new TaskCompletionSource<Task<T>>[inputTasks.Count];
            var results = new Task<Task<T>>[buckets.Length];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new TaskCompletionSource<Task<T>>();
                results[i] = buckets[i].Task;
            }

            int nextTaskIndex = -1;
            Action<Task<T>> continuation = completed =>
            {
                var bucket = buckets[Interlocked.Increment(ref nextTaskIndex)];
                bucket.TrySetResult(completed);
            };

            foreach (var inputTask in inputTasks)
            {
                inputTask.ContinueWith(continuation, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            return results;
        }

        public static async IAsyncEnumerable<TResult> WhenEach<TResult>(this Task<TResult>[] tasks)
        {
            foreach (var bucket in Interleaved(tasks))
            {
                var t = await bucket;
                yield return await t;
            }
        }

        public static ConsoleColor GetColor(this double duration)
        {
            var sec = (int)Math.Round(duration / 1000);
            var color = ConsoleColor.White;
            if (sec <= 2)
            {
                color = ConsoleColor.DarkGreen;
            }
            else if (sec > 2 && sec <= 5)
            {
                color = ConsoleColor.DarkYellow;
            }
            else if (sec > 5 && sec <= 8)
            {
                color = ConsoleColor.Magenta;
            }
            else if (sec > 8)
            {
                color = ConsoleColor.Red;
            }

            return color;
        }

        public static ColorToken GetColorToken(this double duration, char padChar = '.', int max = MaxBarLength)
        {
            var sec = (int)Math.Round(duration / 1000);
            var bar = string.Empty.PadLeft(sec >= 1 ? (sec > max ? max : sec) : 1, padChar);
            return duration.GetColorToken(bar);
        }

        public static ColorToken GetColorToken(this double duration, string barText)
        {
            var coloredBar = barText.On(duration.GetColor());
            return coloredBar;
        }

        public static void DrawTable(this List<Record> records)
        {
            // Credit: https://stackoverflow.com/a/49032729
            // https://github.com/Athari/CsConsoleFormat/blob/master/Alba.CsConsoleFormat.Tests/Elements/Containers/GridTests.cs
            var headerThickness = new LineThickness(LineWidth.Single, LineWidth.Double);
            var rowThickness = new LineThickness(LineWidth.Single, LineWidth.Single);
            var headers = new List<string> { " # ", " url ", " op_Id ", " result ", " ai_duration ", " perfx_duration " };
            var doc = new Document(
                        new Grid
                        {
                            Stroke = new LineThickness(LineWidth.None),
                            StrokeColor = ConsoleColor.DarkGray,
                            Columns =
                            {
                                Enumerable.Range(0, headers.Count).Select(i => new Alba.CsConsoleFormat.Column { Width = GridLength.Auto })
                            },
                            Children =
                            {
                                headers.Select(header => new Cell { Stroke = headerThickness, Color = header.Equals(" perfx_duration ") ? ConsoleColor.DarkGreen : ConsoleColor.White, Background = header.Equals(" perfx_duration ") ? ConsoleColor.White : ConsoleColor.DarkGreen, Children = { header } }),
                                records.Select(record => new[]
                                {
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Right, Children = { record.id } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, Children = { record.url } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.traceId } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.result } },
                                    new Cell { Stroke = rowThickness, Color = record.ai_duration_ms.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.GetDurationString(true, true) + " / " + record.GetDurationInSecString(true, true) } },
                                    new Cell { Stroke = rowThickness, Color = record.duration_ms.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.GetDurationString(suffixUnit: true) + " / " + record.GetDurationInSecString(suffixUnit: true) } }
                                })
                            }
                        }
                     );

            ConsoleRenderer.RenderDocument(doc);
        }

        public static void DrawStats(this List<Record> records)
        {
            records.DrawChart();
            records.DrawPercentilesTable();
        }

        public static void DrawChart(this List<Record> records)
        {
            ColorConsole.WriteLine("\n\n", " Responses ".White().OnGreen());
            var maxIdLength = records.Max(x => x.id.ToString().Length);
            var maxDurationLength = records.Max(x => x.GetDurationInSec());
            foreach (var record in records)
            {
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan());
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan(), record.traceId.DarkGray(), " / ".Green(), record.result.DarkGray(), " / ".Green(), record.url.DarkGray());
                ColorConsole.WriteLine(record.id.ToString().PadLeft(maxIdLength).Green(), " ", VerticalChar.DarkCyan(), record.duration_ms.GetColorToken(' '), " ", record.GetDurationInSecString(), "s".Green());
            }

            ColorConsole.WriteLine(string.Empty.PadLeft(maxIdLength + 1),
                BroderChar.DarkCyan(),
                string.Empty.PadLeft(maxDurationLength > MaxBarLength ? MaxBarLength + 2 : maxDurationLength, HorizontalChar).DarkCyan(),
                "[", "100".DarkCyan(), "]");
        }

        public static void DrawPercentilesChart(this List<Record> records)
        {
            ColorConsole.WriteLine("\n", " Statistics ".White().OnGreen());
            var maxIdLength = 7;
            var maxDurationLength = records.Max(x => x.GetDurationInSec());
            foreach (var group in records.GroupBy(r => r.url))
            {
                ColorConsole.WriteLine("\n ", group.Key.Green());
                var stats = new Dictionary<string, double>
                {
                    { "min", group.Min(x => x.duration_ms) },
                    { "max", group.Max(x => x.duration_ms) },
                    { "mean", group.Select(x => x.duration_ms).Mean() },
                    { "median", group.Select(x => x.duration_ms).Median() },
                    { "std-dev", group.Select(x => x.duration_ms).StandardDeviation() },
                    { "90%", group.Select(x => x.duration_ms).Percentile(90) },
                    { "95%", group.Select(x => x.duration_ms).Percentile(95) },
                    { "99%", group.Select(x => x.duration_ms).Percentile(99) },
                };

                foreach (var record in stats)
                {
                    ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan());
                    ColorConsole.Write(record.Key.PadLeft(maxIdLength).Green());
                    ColorConsole.Write(" ");
                    ColorConsole.Write(VerticalChar.DarkCyan(), record.Value.GetColorToken(' '));
                    ColorConsole.Write(" ");
                    ColorConsole.WriteLine((record.Value / 1000).ToString("F1"), "s".Green());
                }

                ColorConsole.WriteLine(string.Empty.PadLeft(maxIdLength + 1),
                    BroderChar.DarkCyan(),
                    string.Empty.PadLeft(maxDurationLength > MaxBarLength ? MaxBarLength + 2 : maxDurationLength, HorizontalChar).DarkCyan(),
                    "[", "100".DarkCyan(), "]");
            }
        }

        public static void DrawPercentilesTable(this List<Record> records)
        {
            ColorConsole.WriteLine("\n", " Statistics ".White().OnGreen());
            foreach (var group in records.GroupBy(r => r.url))
            {
                ColorConsole.WriteLine("\n ", group.Key.Green());
                var stats = new Dictionary<string, double>
                {
                    { " min ", group.Min(x => x.duration_ms) },
                    { " max ", group.Max(x => x.duration_ms) },
                    { " mean ", group.Select(x => x.duration_ms).Mean() },
                    { " median ", group.Select(x => x.duration_ms).Median() },
                    { " std-dev ", group.Select(x => x.duration_ms).StandardDeviation() },
                    { " 90% ", group.Select(x => x.duration_ms).Percentile(90) },
                    { " 95% ", group.Select(x => x.duration_ms).Percentile(95) },
                    { " 99% ", group.Select(x => x.duration_ms).Percentile(99) },
                };

                var headerThickness = new LineThickness(LineWidth.Single, LineWidth.Double);
                var rowThickness = new LineThickness(LineWidth.Single, LineWidth.Single);
                var doc = new Document(
                            new Grid
                            {
                                Stroke = new LineThickness(LineWidth.None),
                                StrokeColor = ConsoleColor.DarkGray,
                                Columns =
                                {
                                    Enumerable.Range(0, stats.Count).Select(i => new Alba.CsConsoleFormat.Column { Width = GridLength.Auto })
                                },
                                Children =
                                {
                                    stats.Select(stat => new Cell { Stroke = headerThickness, TextAlign = TextAlign.Center, Color = ConsoleColor.Black, Background = ConsoleColor.Gray, Children = { stat.Key } }),
                                    stats.Select(stat => new Cell { Stroke = rowThickness, Color = stat.Value.GetColor(), TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Children = { $" {stat.Value.ToString("F2") }ms " } }),
                                    stats.Select(stat => new Cell { Stroke = rowThickness, Color = stat.Value.GetColor(), TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Children = { $" {(stat.Value / 1000).ToString("F1")}s " } })
                                }
                            }
                         );

                ConsoleRenderer.RenderDocument(doc);
            }
        }

        public static void SaveToFile(this IEnumerable<Record> items, string fileName = ResultsFileName)
        {
            var file = fileName.GetFullPath();
            using (var reader = File.CreateText(file))
            {
                using (var csvWriter = new CsvWriter(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    csvWriter.WriteRecords(items);
                }
            }
        }

        public static IEnumerable<T> ReadResults<T>(string fileName = ResultsFileName)
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

        public static string GetFullPath(this string fileName)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
        }
    }
}
