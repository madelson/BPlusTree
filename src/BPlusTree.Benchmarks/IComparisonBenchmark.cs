namespace Medallion.Collections.Benchmarks;

internal interface IComparisonBenchmark<T>
{
    ImmutableList<T> ImmutableList();
    BPlusTreeImmutableList<T> BPlusTree();
}
