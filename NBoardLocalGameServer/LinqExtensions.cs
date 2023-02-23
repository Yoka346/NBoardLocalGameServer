using System;
using System.Collections.Generic;

namespace NBoardLocalGameServer
{
    internal static class RandomExtensions
    {
        public static void Shuffle<T>(this Random rand, Span<T> span)
        {
            for (var i = span.Length - 1; i > 0; i--)
            {
                var j = rand.Next(i + 1);
                (span[i], span[j]) = (span[j], span[i]);
            }
        }

        public static void Shuffle<T>(this Random rand, List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rand.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
