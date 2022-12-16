namespace Medallion.Collections;

internal sealed partial class BPlusTreeSortedDictionary<TKey, TValue>
{
    internal struct InternalEntry
    {
        internal TKey Key;
        internal Array Child;
        /// <summary>
        /// The number of elements in <see cref="Child"/> which are populated
        /// </summary>
        internal byte ChildElementCount;
    }

    internal struct LeafEntry
    {
        internal TKey Key;
        internal TValue Value;
    }

    private int BinarySearch<T>(ReadOnlySpan<T> span, TKey key)
    {

    }

    private struct
}
