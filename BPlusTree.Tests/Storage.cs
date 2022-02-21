using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    internal static class Storage
    {
        public const int NodeSize = 8;
        public const int SplitNodeSize = NodeSize / 2;

        public static ref T Get<T>(ref Storage8<T> storage, int index)
        {
            Debug.Assert(index is >= 0 and < NodeSize);

            return ref Unsafe.Add(ref Unsafe.As<Storage8<T>, T>(ref storage), index);
        }

        public static ref T Get<T>(ref Storage7<T> storage, int index)
        {
            Debug.Assert(index is >= 0 and < NodeSize - 1);

            return ref Unsafe.Add(ref Unsafe.As<Storage7<T>, T>(ref storage), index);
        }

        public static unsafe Span<T> CreateSpan<T>(ref Storage7<T> storage) =>
            CreateSpan<Storage7<T>, T>(ref storage, 7);

        public static unsafe Span<T> CreateSpan<T>(ref Storage7<T> storage, int length)
        {
            Debug.Assert(length <= 7);
            return CreateSpan<Storage7<T>, T>(ref storage, length);
        }

        public static unsafe Span<T> CreateSpan<T>(ref Storage8<T> storage) =>
            CreateSpan<Storage8<T>, T>(ref storage, 8);

        public static unsafe Span<T> CreateSpan<T>(ref Storage8<T> storage, int length)
        {
            Debug.Assert(length <= 8);
            return CreateSpan<Storage8<T>, T>(ref storage, length);
        }

        private static unsafe Span<TElement> CreateSpan<TRef, TElement>(ref TRef storage, int length) =>
#if NET6_0_OR_GREATER
            MemoryMarshal.CreateSpan(ref Unsafe.As<TRef, TElement>(ref storage), length);
#else
            new(Unsafe.AsPointer(ref storage), 7);
#endif
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Storage7<T>
    {
        internal T Item1;
        internal T Item2;
        internal T Item3;
        internal T Item4;
        internal T Item5;
        internal T Item6;
        internal T Item7;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Storage8<T>
    {
        internal Storage7<T> Items1To7;
        internal T Item8;
    }
}
