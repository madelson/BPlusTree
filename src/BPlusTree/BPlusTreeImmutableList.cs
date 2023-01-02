namespace Medallion.Collections;

public static class BPlusTreeImmutableList
{
    public static BPlusTreeImmutableList<T> CreateRange<T>(IEnumerable<T> items) =>
        BPlusTreeImmutableList<T>.Empty.AddRange(items);
}
