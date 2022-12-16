namespace Medallion.Collections;

internal sealed partial class BPlusTreeSortedDictionary<TKey, TValue>
{
    public IComparer<TKey> Comparer { get; }
}
