namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Alba.CsConsoleFormat;

    using ByteSizeLib;

    using ColoredConsole;

    using MathNet.Numerics.Statistics;

    public static class Utils
    {
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

        public static ConsoleColor GetColor(this double duration, int divider = 1000)
        {
            var sec = (int)Math.Round(duration / divider);
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

        public static ColorToken GetColorToken(this string result, string barText = null)
        {
            return barText == null ? result.Color(result.GetColor()) : barText.On(result.GetColor());
        }

        public static ConsoleColor GetColor(this string result)
        {
            return result.Contains("200") ? ConsoleColor.DarkGreen : (result.Contains(": ") ? ConsoleColor.DarkYellow : ConsoleColor.Red);
        }


        public static ColorToken GetColorToken(this long? size, string barText)
        {
            return barText.On(size.GetColor());
        }

        public static ConsoleColor GetColor(this long? size)
        {
            if (!size.HasValue)
            {
                return ConsoleColor.DarkGray;
            }

            var kb = ByteSizeLib.ByteSize.FromBytes(size.Value).KiloBytes;
            var color = ConsoleColor.White;
            if (kb <= 200)
            {
                color = ConsoleColor.DarkGreen;
            }
            else if (kb > 200 && kb <= 500)
            {
                color = ConsoleColor.DarkYellow;
            }
            else if (kb > 500 && kb <= 800)
            {
                color = ConsoleColor.Magenta;
            }
            else if (kb > 800)
            {
                color = ConsoleColor.Red;
            }

            return color;
        }

        public static void DrawTable(this List<Record> records)
        {
            // Credit: https://stackoverflow.com/a/49032729
            // https://github.com/Athari/CsConsoleFormat/blob/master/Alba.CsConsoleFormat.Tests/Elements/Containers/GridTests.cs
            var headerThickness = new LineThickness(LineWidth.Single, LineWidth.Double);
            var rowThickness = new LineThickness(LineWidth.Single, LineWidth.Single);
            var headers = new List<string> { " # ", " url ", " op_Id ", " result ", " size ", " ai ", " perfx " };
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
                                headers.Select(header => new Cell { Stroke = headerThickness, Color = header.Equals(" perfx ") ? ConsoleColor.DarkGreen : ConsoleColor.White, Background = header.Equals(" perfx ") ? ConsoleColor.White : ConsoleColor.DarkGreen, Children = { header } }),
                                records.Select(record => new[]
                                {
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Right, Children = { record.id } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, Children = { record.url } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.op_Id } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.result }, Color = record.result.GetColor() },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.size_str }, Color = record.size_b.GetColor() },
                                    new Cell { Stroke = rowThickness, Color = record.ai_ms.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.ai_s_str + "s" } },
                                    new Cell { Stroke = rowThickness, Color = record.local_ms.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { record.local_s_str + "s" } }
                                })
                            }
                        }
                     );

            ConsoleRenderer.RenderDocument(doc);
        }

        public static void DrawStats(this List<Record> records)
        {
            records.DrawTable();
            records.DrawChart();
            records.DrawPercentilesTable();
        }

        public static void DrawChart(this List<Record> records)
        {
            ColorConsole.WriteLine("\n\n", " Responses ".White().OnGreen());
            var maxIdLength = records.Max(x => x.id.ToString().Length);
            var maxDurationLength = records.Max(x => x.duration_s_round);
            foreach (var record in records)
            {
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan());
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan(), record.op_Id.DarkGray(), " / ", record.size_str.Color(record.size_b.GetColor()), " / ", record.result.GetColorToken(), " / ".Green(), record.url.DarkGray());
                ColorConsole.WriteLine(record.id.ToString().PadLeft(maxIdLength).Green(), " ", VerticalChar.DarkCyan(), record.duration_ms.GetColorToken(' '), " ", record.duration_s_str, "s".Green());
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
            var maxDurationLength = records.Max(x => x.duration_s_round);
            foreach (var group in records.GroupBy(r => r.url + (string.IsNullOrEmpty(r.ai_op_Id) ? string.Empty : $" (ai)")))
            {
                ColorConsole.WriteLine("\n ", group.Key.Green());
                var okRecords = group.Where(x => x.result.Contains("200"));
                var stats = new Dictionary<string, double>
                {
                    { " dur-min ", okRecords.Min(x => x.duration_ms) },
                    { " dur-max ", okRecords.Max(x => x.duration_ms) },
                    { " dur-mean ", okRecords.Select(x => x.duration_ms).Mean() },
                    { " dur-median ", okRecords.Select(x => x.duration_ms).Median() },
                    { " dur-std-dev ", okRecords.Select(x => x.duration_ms).StandardDeviation() },
                    { " dur-90% ", okRecords.Select(x => x.duration_ms).Percentile(90) },
                    { " dur-95% ", okRecords.Select(x => x.duration_ms).Percentile(95) },
                    { " dur-99% ", okRecords.Select(x => x.duration_ms).Percentile(99) },
                    { " size-min ", okRecords.Min(x => x.size_b.HasValue ? x.size_b.Value : 0) },
                    { " size-max ", okRecords.Max(x => x.size_b.HasValue ? x.size_b.Value : 0) },
                    { " 200-ok ", (int)Math.Round(((double)(okRecords.Count() / group.Count())) * 100) },
                    { " xxx-other ", (100 - (int)Math.Round(((double)(okRecords.Count() / group.Count())) * 100)) }
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
            var runs = new List<Run>();
            foreach (var group in records.GroupBy(r => r.url + (string.IsNullOrEmpty(r.ai_op_Id) ? string.Empty : $" (ai)")))
            {
                var run = new Run(group, group.Key);
                runs.Add(run);
            }

            foreach (var run in runs)
            {
                ColorConsole.WriteLine("\n ", run.url.Green());
                var headerThickness = new LineThickness(LineWidth.Single, LineWidth.Double);
                var rowThickness = new LineThickness(LineWidth.Single, LineWidth.Single);
                var doc = new Document(
                            new Grid
                            {
                                Stroke = new LineThickness(LineWidth.None),
                                StrokeColor = ConsoleColor.DarkGray,
                                Columns =
                                {
                                    Enumerable.Range(0, run.Properties.Count).Select(i => new Alba.CsConsoleFormat.Column { Width = GridLength.Auto })
                                },
                                Children =
                                {
                                    run.Properties.Select(prop => new Cell { Stroke = headerThickness, TextAlign = TextAlign.Center, Color = ConsoleColor.Black, Background = ConsoleColor.Gray, Children = { $" {prop.Name} " } }),
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_min_s.GetColor(1), Children = { run.dur_min_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_max_s.GetColor(1), Children = { run.dur_max_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_mean_s.GetColor(1), Children = { run.dur_mean_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_median_s.GetColor(1), Children = { run.dur_median_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_std_dev_s.GetColor(1), Children = { run.dur_std_dev_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_90_per_s.GetColor(1), Children = { run.dur_90_per_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_95_per_s.GetColor(1), Children = { run.dur_95_per_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_99_per_s.GetColor(1), Children = { run.dur_99_per_s + "s"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.size_min_kb.GetColor(100), Children = { run.size_min_kb + "KB"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.size_max_kb.GetColor(100), Children = { run.size_max_kb + "KB"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = ConsoleColor.DarkGreen, Children = { run.ok_200 + "%"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = ConsoleColor.DarkYellow, Children = { run.other_xxx + "%"} },
                                }
                            });

                ConsoleRenderer.RenderDocument(doc);
            }
        }

        public static string GetFullPath(this string fileName)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
        }
    }
}
