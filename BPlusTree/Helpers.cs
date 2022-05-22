using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
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

        public static ReadOnlySpan<T> CreateReadOnlySpan<T>(ref T value)
        {
#if NET
            return MemoryMarshal.CreateReadOnlySpan(ref value, 1);
#else
            return new T[] { value };
#endif
        }
    }
}
