using System;
using System.Threading;

namespace Recipe.NetCore.Helper
{
    public static class IdHelper
    {
        public static dynamic Generate()
        {
            return Convert.ToInt64(string.Format("{0}{1}", (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", ""), StaticRandom.Rand()));
        }
    }
    /// <summary>
    /// Protection from Multi-thread
    /// </summary>
    public static class StaticRandom
    {
        static int seed = Environment.TickCount;

        static readonly ThreadLocal<Random> random =
            new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static string Rand()
        {
            return random.Value.Next(0, 9999).ToString("D4");
        }
    }
}
