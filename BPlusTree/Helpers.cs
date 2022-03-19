using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    internal static class Helpers
    {
        public static T[] Copy<T>(this T[] array)
        {
            var copy = new T[array.Length];
            array.AsSpan().CopyTo(copy);
            return copy;
        }
    }
}
