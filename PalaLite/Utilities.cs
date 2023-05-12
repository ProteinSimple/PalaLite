using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtensionMethods
{
    public static class Utilities
    {
        /// <summary>
        /// Restricts a value between a minimum and maximum.
        /// </summary>
        /// <typeparam name="T">The type <p>T</p> of the value, minimum, and maximum inputs.  <p>T</p> must impoment IComparable</typeparam>
        /// <param name="val">The value to compare of type <p>T</p></param>
        /// <param name="min">The minimum value to compare to of type <p>T</p></param>
        /// <param name="max">The maximum value to compare to of type <p>T</p></param>
        /// <returns>The clamped value with type <p>T</p>.</returns>
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
        public static bool CheckBounds<T>(this T val, T min, T max) where T : IComparable<T>
        {
            return (val.CompareTo(min) >= 0) && (val.CompareTo(max) <= 0);
        }

        /// <summary>
        /// Break a list of items into chunks of a specific size
        /// From https://www.techiedelight.com/split-a-list-into-sublists-of-size-n-in-csharp/
        /// </summary>
        public static List<List<T>> Chunk<T>(this List<T> values, int chunkSize)
        {
            var chunks = new List<List<T>>();
            for (int i = 0; i < values.Count; i += chunkSize)
            {
                chunks.Add(values.GetRange(i, Math.Min(chunkSize, values.Count - i)));
            }
            return chunks;
        }
    }
}