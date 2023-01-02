using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Medallion.Collections;

public partial class BPlusTreeImmutableList<T>
{
    private const int MaxIndexNodeSize = 8, MinIndexNodeSize = MaxIndexNodeSize / 2;

    private static int MaxLeafNodeSize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            Unsafe.SizeOf<T>() == 1 ? 128
            : Unsafe.SizeOf<T>() == 2 ? 64
            : Unsafe.SizeOf<T>() == 4 ? 32
            : Unsafe.SizeOf<T>() == 8 ? 16
            : 8;
    }

    private static int MinLeafNodeSize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MaxLeafNodeSize / 2;
    }

    // todo builder
    private static int GetCount(IndexEntry[] index) => index[index.Length - 1].Offset;

    [DebuggerDisplay("{Item}")]
    private struct LeafEntry 
    { 
        public T Item;

        [DebuggerStepThrough]
        public LeafEntry(T item) { this.Item = item; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    private static LeafEntry[] ToLeaf(Array node)
    {
        Debug.Assert(node is LeafEntry[]);
        return Unsafe.As<LeafEntry[]>(node);
    }
}

[DebuggerDisplay("Offset = {Offset}")]
internal struct IndexEntry
{
    private const int CumulativeChildCountMask = int.MaxValue;
    private const int IsChildMutableMask = ~int.MaxValue;

    [DebuggerStepThrough]
    public IndexEntry(Array child, int offset)
    {
        Child = child;
        Offset = offset;
    }

    public Array Child;
    public int Offset;
}

internal static class Helpers
{
    [DoesNotReturn]
    public static void ThrowIndexOutOfRange() =>
        ThrowIndexOutOfRange("Index was out of range. Must be non-negative and less than the size of the collection.");

    [DoesNotReturn]
    public static void ThrowIndexOutOfRange(string message) =>
        throw new ArgumentOutOfRangeException("index", message);

    [DoesNotReturn]
    public static void ThrowCountOverflow() =>
        throw new OverflowException($"{nameof(BPlusTreeImmutableList)} cannot store more than {int.MaxValue} elements");

    public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
        {
            ThrowArgumentNull(paramName);
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentNull(string? paramName) => throw new ArgumentNullException(paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static bool IsIndexNode(this Array node) => node.GetType() == typeof(IndexEntry[]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static IndexEntry[] ToIndex(this Array node)
    {
        Debug.Assert(node is IndexEntry[]);
        return Unsafe.As<IndexEntry[]>(node);
    }

    public static int FindEntry(this IndexEntry[] node, int index)
    {
        Debug.Assert(node.Length > 0);
        Debug.Assert(index < node[node.Length - 1].Offset); // todo builder

        int length = node.Length;
#if NET5_0_OR_GREATER
        ref IndexEntry firstElement = ref MemoryMarshal.GetArrayDataReference(node);
#else
        ref IndexEntry firstElement = ref node[0];
#endif
        int mid = length >> 1;

        // todo builder
        if (index >= Unsafe.Add(ref firstElement, mid).Offset)
        {
            for (var i = mid + 1; i < length - 1; ++i)
            {
                if (index < Unsafe.Add(ref firstElement, i).Offset) { return i; }
            }
            return length - 1;
        }

        for (var i = 0; i < mid; ++i)
        {
            if (index < Unsafe.Add(ref firstElement, i).Offset) { return i; }
        }
        return mid;
    }
}