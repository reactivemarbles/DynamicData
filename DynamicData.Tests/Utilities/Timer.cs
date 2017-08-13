using System;
using System.Diagnostics;

namespace DynamicData.Tests.Utilities
{
    public class Timer
    {
        // evaluates a method the specified number of times and print performance information
        public static void ToConsole2(Action method, long times = 1, string label = "Timing")
        {
            const string template = "Run {0}x.  Total= {1}ms.    Avg= {2}ms.   Current per second={3}";
            var sw = new Stopwatch();
            sw.Start();
            for (long i = 0; i < times; i++)
            {
                method();
            }
            sw.Stop();

            var avg = Math.Round((decimal)sw.Elapsed.TotalMilliseconds / (decimal)times, 8);
            var persecond = (int)((1 / avg) * 1000);
            Console.WriteLine(template, times, sw.Elapsed.TotalMilliseconds, avg, persecond);
        }

        public static void ToConsole(Action method, long times = 1, string label = "Timing")
        {
            const string template = "[{4}] Run {0}x.  Total= {1}ms.    Avg= {2}ms.   Current per second={3}";

            //run once to ensure cahing
            //   method();

            var sw = Stopwatch.StartNew();
            for (long i = 0; i < times; i++)
            {
                method();
            }
            sw.Stop();

            var duration = sw.ElapsedMilliseconds;

            // avg = sw.ElapsedTicks /times;
            var avgticks = Math.Round((decimal)sw.ElapsedTicks / (decimal)times, 8);
            var avgelapsed = TimeSpan.FromTicks((long)avgticks);

            var avg = sw.Elapsed.TotalMilliseconds / times;

            var persecond = (int)((1 / avg) * 1000);
            Console.WriteLine(template, times, sw.Elapsed.TotalMilliseconds, avg, persecond, label);
        }

        //private decimal FractionalMilliseconds(TimeSpan span)
        //{
        //    var 
        //}
    }
}
