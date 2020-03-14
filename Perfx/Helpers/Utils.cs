namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Alba.CsConsoleFormat;

    using ColoredConsole;

    public static class Utils
    {
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

        public static ColorToken GetColorToken(this double duration)
        {
            var sec = (int)Math.Round(duration / 1000);
            var bar = string.Empty.PadLeft(sec > 1 ? sec : 1, '.');
            var coloredBar = bar.OnGreen();
            if (sec <= 2)
            {
                coloredBar = bar.OnGreen();
            }
            else if (sec > 2 && sec <= 5)
            {
                coloredBar = bar.OnDarkYellow();
            }
            else if (sec > 5 && sec <= 8)
            {
                coloredBar = bar.OnMagenta();
            }
            else if (sec > 8)
            {
                coloredBar = bar.OnRed();
            }

            return coloredBar;
        }

        public static void DrawTable(this List<LogRecord> aiLogs, List<(string traceId, double duration)> traceIds)
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
                                        aiLogs.Select(log => new[]
                                        {
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Right, Children = { log.id } },
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, Children = { log.url } },
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { log.operation_ParentId } },
                                            new Cell { Stroke = rowThickness, TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { log.resultCode } },
                                            new Cell { Stroke = rowThickness, Color = log.duration.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { log.duration.ToString("F2") + "ms / " + $"{(log.duration / 1000.00).ToString("F1", CultureInfo.InvariantCulture)}s" } },
                                            new Cell { Stroke = rowThickness, Color = traceIds.SingleOrDefault(t => t.traceId.Equals(log.operation_ParentId, StringComparison.OrdinalIgnoreCase)).duration.GetColor(), TextWrap = TextWrap.NoWrap, TextAlign = TextAlign.Center, Children = { traceIds.SingleOrDefault(t => t.traceId.Equals(log.operation_ParentId, StringComparison.OrdinalIgnoreCase)).duration.ToString("F2") + "ms / " + $"{(traceIds.SingleOrDefault(t => t.traceId.Equals(log.operation_ParentId, StringComparison.OrdinalIgnoreCase)).duration / 1000.00).ToString("F1", CultureInfo.InvariantCulture)}s" } }
                                        })
                            }
                        }
                     );

            ConsoleRenderer.RenderDocument(doc);
        }

        public static void DrawChart(this List<(string traceId, double duration)> traceIds)
        {
            var maxLength = traceIds.Max(x => x.traceId.Length);
            foreach (var item in traceIds)
            {
                ColorConsole.Write(item.traceId.PadLeft(maxLength));
                ColorConsole.Write(" ");
                ColorConsole.Write(item.duration.GetColorToken());
                ColorConsole.Write(" ");
                ColorConsole.WriteLine(item.duration.ToString());
            }
        }
    }
}
