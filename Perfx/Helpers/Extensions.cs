namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Alba.CsConsoleFormat;

    using ColoredConsole;

    using Microsoft.Extensions.Logging;

    public static class Extensions
    {
        private const int MaxBarLength = 100;
        private const string VerticalChar = "│"; // "┃"
        private const char HorizontalChar = '─'; // '━'
        private const string BroderChar = "└"; // "┗"
        private static string userName;

        public static string UserName
        {
            get
            {
                if (string.IsNullOrEmpty(userName))
                {
                    if (string.IsNullOrWhiteSpace(Environment.UserName))
                    {
                        userName = $@"{Environment.MachineName ?? string.Empty}\{Environment.GetEnvironmentVariable("USERNAME") ?? string.Empty}";
                    }
                    else
                    {
                        userName = $@"{Environment.MachineName ?? string.Empty}\{Environment.UserName}";
                    }
                }

                return userName;
            }
        }

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

        public static ConsoleColor GetColor(this double durationOrSize, double expectedSla, int divider = 1000)
        {
            var actual = durationOrSize / divider;
            var color = ConsoleColor.White;
            if (actual <= expectedSla)
            {
                color = ConsoleColor.DarkGreen;
            }
            else if (actual > expectedSla && actual <= expectedSla * 2)
            {
                color = ConsoleColor.DarkYellow;
            }
            else if (actual > expectedSla * 2 && actual <= expectedSla * 4)
            {
                color = ConsoleColor.Magenta;
            }
            else if (actual > expectedSla * 4)
            {
                color = ConsoleColor.Red;
            }

            return color;
        }

        public static ColorToken GetColorTokenForDuration(this double duration, double expectedSla, char padChar = '.', int max = MaxBarLength)
        {
            var sec = (int)Math.Round(duration / 1000);
            var bar = string.Empty.PadLeft(sec >= 1 ? (sec > max ? max : sec) : 1, padChar);
            return duration.GetColorTokenForDuration(expectedSla, bar);
        }

        public static ColorToken GetColorTokenForDuration(this double duration, double expectedSla, string barText)
        {
            var coloredBar = barText.On(duration.GetColor(expectedSla));
            return coloredBar;
        }

        public static ColorToken GetColorTokenForStatusCode(this string result, string barText = null)
        {
            return barText == null ? result.Color(result.GetColorForStatusCode()) : barText.On(result.GetColorForStatusCode());
        }

        public static ConsoleColor GetColorForStatusCode(this string result)
        {
            return result.Contains("200") ? ConsoleColor.DarkGreen : (result.Contains(": ") ? ConsoleColor.DarkYellow : ConsoleColor.Red);
        }


        public static ColorToken GetColorTokenForSize(this long? size, double expectedSize, string barText)
        {
            return barText.On(size.GetColorForSize(expectedSize));
        }

        public static ConsoleColor GetColorForSize(this long? size, double expectedSize)
        {
            if (!size.HasValue)
            {
                return ConsoleColor.DarkGray;
            }

            var kb = ByteSizeLib.ByteSize.FromBytes(size.Value).KiloBytes;
            var color = ConsoleColor.White;
            if (kb <= expectedSize)
            {
                color = ConsoleColor.DarkGreen;
            }
            else if (kb > expectedSize && kb <= expectedSize * 2)
            {
                color = ConsoleColor.DarkYellow;
            }
            else if (kb > expectedSize * 2 && kb <= expectedSize * 4)
            {
                color = ConsoleColor.Magenta;
            }
            else if (kb > expectedSize * 4)
            {
                color = ConsoleColor.Red;
            }

            return color;
        }

        public static void DrawTable(this IList<Result> results)
        {
            // Credit: https://stackoverflow.com/a/49032729
            // https://github.com/Athari/CsConsoleFormat/blob/master/Alba.CsConsoleFormat.Tests/Elements/Containers/GridTests.cs
            var headerThickness = new LineThickness(LineWidth.Single, LineWidth.Double);
            var rowThickness = new LineThickness(LineWidth.Single, LineWidth.Single);
            var headers = new List<string> { " # ", " url ", " query ", " op_Id ", " result ", " size ", " ai ", " perfx " };
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
                                results.Select(result => new[]
                                {
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Right, Children = { result.id } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, Children = { result.url } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, Children = { result.query } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { result.op_Id } },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { result.result }, Color = result.result.GetColorForStatusCode() },
                                    new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { result.size_str }, Color = result.size_b.GetColorForSize(result.sla_size_kb) },
                                    new Cell { Stroke = rowThickness, Color = result.ai_ms.GetColor(result.sla_dur_s), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { result.ai_s_str + "s" } },
                                    new Cell { Stroke = rowThickness, Color = result.local_ms.GetColor(result.sla_dur_s), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { result.local_s_str + "s" } }
                                })
                            }
                        }
                     );

            ConsoleRenderer.RenderDocument(doc);
        }

        public static void DrawStats(this IList<Result> results)
        {
            results.DrawTable();
            results.DrawChart();
            results.DrawPercentilesTable();
        }

        public static void DrawChart(this IList<Result> results)
        {
            ColorConsole.WriteLine("\n\n", " Responses ".White().OnGreen());
            var maxIdLength = results.Max(x => x.id.ToString().Length);
            var maxDurationLength = results.Max(x => x.duration_s_round);
            foreach (var result in results)
            {
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan());
                ColorConsole.WriteLine(VerticalChar.PadLeft(maxIdLength + 2).DarkCyan(), result.op_Id.DarkGray(), " / ", result.result.GetColorTokenForStatusCode(), " / ", result.full_url.DarkGray());
                ColorConsole.WriteLine(result.id.ToString().PadLeft(maxIdLength).Green(), " ", VerticalChar.DarkCyan(), result.duration_ms.GetColorTokenForDuration(' '), " ", result.duration_s_str, "s".Green(), " / ", result.size_str.Color(result.size_b.GetColorForSize(result.sla_size_kb)), $" (".White(), $"sla: {result.sla_dur_s}s".DarkGray(), " / ".White(), $"{result.sla_size_kb}Kb".DarkGray(), ")".White());
            }

            var minBarLength = (10 - maxDurationLength % 10) + maxDurationLength;
            ColorConsole.WriteLine(string.Empty.PadLeft(maxIdLength + 1),
                BroderChar.DarkCyan(),
                string.Empty.PadLeft(maxDurationLength > MaxBarLength ? MaxBarLength + 2 : minBarLength, HorizontalChar).DarkCyan(),
                "[", $"{(maxDurationLength > MaxBarLength ? MaxBarLength : minBarLength)}".DarkCyan(), "]");
        }

        public static void DrawPercentilesTable(this IList<Result> results)
        {
            ColorConsole.WriteLine("\n", " Statistics ".White().OnGreen());
            var runs = new List<Run>();
            foreach (var group in results.GroupBy(r => r.url + (string.IsNullOrEmpty(r.ai_op_Id) ? string.Empty : $" (ai)")))
            {
                if (group.Key != null)
                {
                    var run = new Run(group, group.Key);
                    runs.Add(run);
                }
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
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = ConsoleColor.DarkGray, Children = { run.sla_dur_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = ConsoleColor.DarkGray, Children = { run.sla_size_kb } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_min_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_min_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_max_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_max_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_mean_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_mean_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_median_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_median_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_std_dev_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_std_dev_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_90_perc_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_90_perc_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_95_perc_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_95_perc_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.dur_99_perc_s.GetColor(run.sla_dur_s, 1), Children = { run.dur_99_perc_s } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.size_min_kb.GetColor(run.sla_size_kb, 1), Children = { run.size_min_kb } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = run.size_max_kb.GetColor(run.sla_size_kb, 1), Children = { run.size_max_kb } },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = ConsoleColor.DarkGreen, Children = { run.ok_200 + "%"} },
                                    new Cell { Stroke = rowThickness, TextAlign = TextAlign.Center, TextWrap = TextWrap.NoWrap, Color = ConsoleColor.DarkYellow, Children = { run.other_xxx + "%"} },
                                }
                            });

                ConsoleRenderer.RenderDocument(doc);
            }
        }

        public static void ShowThreads(this ILogger logger, bool consoleOutput = false)
        {
            try
            {
                var threads = Process.GetCurrentProcess().Threads.Cast<ProcessThread>().ToList();
                if (consoleOutput)
                {
                    ColorConsole.WriteLine($"Threads: ", threads.Count.ToString().Cyan());
                    threads.ForEach(t => ColorConsole.WriteLine($" {t.Id}".PadLeft(8).DarkGray(), ": ".Cyan(), $"{t.ThreadState} - {(t.ThreadState == System.Diagnostics.ThreadState.Wait ? t.WaitReason.ToString() : string.Empty)}".DarkGray()));
                }

                logger.LogDebug($"Threads: {threads.Count}");
                threads.ForEach(t => logger.LogDebug($"\t{t.Id}: {t.ThreadState} - {(t.ThreadState == System.Diagnostics.ThreadState.Wait ? t.WaitReason.ToString() : string.Empty)}"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }

        // Credit: https://softwareengineering.stackexchange.com/a/370702
        public static async Task ForEachAsyncConcurrent<T>(
            this IEnumerable<T> enumerable,
            Func<T, Task> action,
            int? maxDegreeOfParallelism = null)
        {
            if (maxDegreeOfParallelism.HasValue)
            {
                using (var semaphoreSlim = new SemaphoreSlim(
                    maxDegreeOfParallelism.Value, maxDegreeOfParallelism.Value))
                {
                    var tasksWithThrottler = new List<Task>();

                    foreach (var item in enumerable)
                    {
                        // Increment the number of currently running tasks and wait if they are more than limit.
                        await semaphoreSlim.WaitAsync();

                        tasksWithThrottler.Add(Task.Run(async () =>
                        {
                            await action(item).ContinueWith(res =>
                            {
                                // action is completed, so decrement the number of currently running tasks
                                semaphoreSlim.Release();
                            });
                        }));
                    }

                    // Wait for all tasks to complete.
                    await Task.WhenAll(tasksWithThrottler.ToArray());
                }
            }
            else
            {
                await Task.WhenAll(enumerable.Select(item => action(item)));
            }
        }

        public static (string statsFile, List<Run> stats) GetStats<T>(this IEnumerable<T> results, string file)
        {
            return (file.Replace("Results", "Stats"), results.GetStats<T>());
        }

        public static List<Run> GetStats<T>(this IEnumerable<T> results)
        {
            var stats = new List<Run>();
            foreach (var group in results.Cast<Result>().GroupBy(x => x.url + (string.IsNullOrWhiteSpace(x.ai_op_Id) ? string.Empty : " (ai)")))
            {
                if (group.Key != null)
                {
                    var run = new Run(group.ToList(), group.Key);
                    stats.Add(run);
                }
            }

            return stats;
        }

        public static bool Overwrite(this string file, bool overwrite)
        {
            if (!overwrite && File.Exists(file))
            {
                ColorConsole.Write("\n> ".Red(), "Overwrite ", file.DarkYellow(), "?", " (Y/N) ".Red());
                var quit = Console.ReadKey();
                ColorConsole.WriteLine();
                return quit.Key == ConsoleKey.Y;
            }
            else
            {
                ColorConsole.WriteLine("\n> ".Green(), "Saving output to: ", file.DarkGray());
            }

            return true;
        }

        public static string GetConnString(this IOutput output, Settings settings)
        {
            return settings.Outputs.FirstOrDefault(x => output.GetType().Name.StartsWith(x.Format.ToString()))?.ConnString;
        }

        public static async Task<bool> Save(this IEnumerable<IOutput> outputs, IList<Result> results, Settings settings)
        {
            var result = true;
            foreach (var output in outputs)
            {
                try
                {
                    result = await output.Save(results, settings) && result;
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.White().OnRed());
                }
            }

            return result;
        }
    }
}
