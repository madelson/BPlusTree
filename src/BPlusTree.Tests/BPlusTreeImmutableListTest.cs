namespace Medallion.Collections.Tests;

public class BPlusTreeImmutableListTest
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(10000)]
    public void CreateRange(int count)
    {
        string[] values = Enumerable.Range(0, count).Select(i => i.ToString()).ToArray();
        BPlusTreeImmutableList<string> list = BPlusTreeImmutableList.CreateRange(values);
        Assert.Equal(values, list);
    }

    [Fact]
    public void AddRangeWithEmptyList()
    {
        BPlusTreeImmutableList<bool?> empty = BPlusTreeImmutableList<bool?>.Empty;
        Assert.Same(empty, empty.AddRange(Enumerable.Empty<bool?>()));

        BPlusTreeImmutableList<bool?> list = BPlusTreeImmutableList.CreateRange(new bool?[] { false, true, null });
        Assert.Same(list, list.AddRange(empty));
        Assert.Same(list, empty.AddRange(list));
    }

    [Theory, CombinatorialData]
    public void AddRange(
        [CombinatorialValues(1, 7, 49, 123, 10000)] int startCount,
        [CombinatorialValues(1, 7, 49, 123, 10000)] int addCount,
        bool addAsBPlusTree)
    {
        string[] startingValues = Enumerable.Range(0, startCount).Select(i => $"s{i}").ToArray();
        string[] addedValues = Enumerable.Range(0, addCount).Select(i => $"a{i}").ToArray();
        BPlusTreeImmutableList<string> list = BPlusTreeImmutableList.CreateRange(startingValues);
        list = list.AddRange(addAsBPlusTree ? BPlusTreeImmutableList.CreateRange(addedValues) : addedValues);
        if (!list.SequenceEqual(startingValues.Concat(addedValues)))
        {
            var arr = startingValues.Concat(addedValues).ToArray();
            var fmm = Enumerable.Range(0, arr.Length).First(i => arr[i] != list[i]);
            var x = new { E = arr[fmm], A = list[fmm], fmm };
        }
        Assert.Equal(startingValues.Concat(addedValues), list);
    }
}
