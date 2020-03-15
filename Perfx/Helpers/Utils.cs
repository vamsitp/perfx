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
                color = ConsoleColor.Green;
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
            var sec = (int)Math.Round(duration / 1000);
            var coloredBar = barText.OnGreen();
            if (sec <= 2)
            {
                coloredBar = barText.OnDarkGreen();
            }
            else if (sec > 2 && sec <= 5)
            {
                coloredBar = barText.OnDarkYellow();
            }
            else if (sec > 5 && sec <= 8)
            {
                coloredBar = barText.OnMagenta();
            }
            else if (sec > 8)
            {
                coloredBar = barText.OnRed();
            }

            return coloredBar;
        }

        public static void DrawTable(this List<Record> records)
        {
            // Credit: https://stackoverflow.com/a/49032729
            // https://github.com/Athari/CsConsoleFormat/blob/master/Alba.CsConsoleFormat.Tests/Elements/Containers/GridTests.cs
            var headerThickness = new LineThickness(LineWidth.Single, LineWidth.Double);
            var rowThickness = new LineThickness(LineWidth.Single, LineWidth.Single);
            var doc = new Document(
                        new Grid
                        {
                            Stroke = new LineThickness(LineWidth.None),
                            StrokeColor = ConsoleColor.DarkGray,
                            Columns =
                            {
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto, MaxWidth = 200 },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto },
                                        new Alba.CsConsoleFormat.Column { Width = GridLength.Auto }
                            },
                            Children = {
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " # " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " url " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " op_Id " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " result " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.White, Background = ConsoleColor.DarkGreen, Children = { " ai_duration " } },
                                        new Cell { Stroke = headerThickness, Color = ConsoleColor.DarkGreen, Background = ConsoleColor.White, Children = { " perfx_duration " } },
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

        public static void DrawCharts(this List<Record> records)
        {
            records.DrawChart();
            records.DrawPercentilesChart();
        }

        public static void DrawChart(this List<Record> records)
        {
            ColorConsole.WriteLine("\n", " RESPONSES ".White().OnDarkCyan(), " ");
            var maxIdLength = records.Max(x => $"{x.id.ToString()} {x.url}".Length);
            var maxDurationLength = records.Max(x => x.GetDurationInSec());
            foreach (var record in records)
            {
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan()); // , record.traceId
                ColorConsole.Write(record.id.ToString().Green(), record.url.PadLeft(maxIdLength - record.id.ToString().Length));
                ColorConsole.Write(" ");
                ColorConsole.Write(VerticalChar.DarkCyan(), record.duration_ms.GetColorToken(' '));
                ColorConsole.Write(" ");
                ColorConsole.WriteLine(record.GetDurationInSecString(), "s".Green());
            }

            ColorConsole.WriteLine(string.Empty.PadLeft(maxIdLength + 1),
                BroderChar.DarkCyan(),
                string.Empty.PadLeft(maxDurationLength > MaxBarLength ? MaxBarLength + 2 : maxDurationLength, HorizontalChar).DarkCyan(),
                "[", "100".DarkCyan(), "]");
        }

        public static void DrawPercentilesChart(this List<Record> records)
        {
            ColorConsole.WriteLine("\n", " STATS ".White().OnDarkCyan(), " ");
            var maxIdLength = 7;
            var maxDurationLength = records.Max(x => x.GetDurationInSec());
            var stats = new Dictionary<string, double>
            {
                { "min", records.Min(x => x.duration_ms) },
                { "max", records.Max(x => x.duration_ms) },
                { "avg", records.Average(x => x.duration_ms) },
                { "std-dev", records.Select(x => x.duration_ms).StandardDeviation() },
                { "50%", records.Select(x => x.duration_ms).Percentile(50) },
                { "90%", records.Select(x => x.duration_ms).Percentile(90) },
                { "95%", records.Select(x => x.duration_ms).Percentile(95) },
                { "99%", records.Select(x => x.duration_ms).Percentile(99) },
            };

            foreach (var record in stats)
            {
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan()); // , record.traceId
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
