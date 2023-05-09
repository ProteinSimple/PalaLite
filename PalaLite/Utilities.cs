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
        private static bool IsSubArrayEqual(byte[] x, byte[] y, int start)
        {
            for (int i = 0; i < y.Length; i++)
            {
                if (x[start++] != y[i]) return false;
            }
            return true;
        }
        public static int StartingIndex(this byte[] x, byte[] y)
        {
            int max = 1 + x.Length - y.Length;
            for (int i = 0; i < max; i++)
            {
                if (IsSubArrayEqual(x, y, i)) return i;
            }
            return -1;
        }
    }
}