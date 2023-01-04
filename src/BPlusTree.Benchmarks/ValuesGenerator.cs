namespace Medallion.Collections.Benchmarks;

internal static class ValuesGenerator
{
    public static IEnumerable<T> UniqueValues<T>(int count)
    {
        var random = new Random(123456);
        return Enumerable.Range(0, count)
            .OrderBy(_ => random.Next())
            .Select(
                i => typeof(int) == typeof(T) ? (T)(object)i
                    : typeof(string) == typeof(T) ? (T)(object)i.ToString()
                    : throw new NotSupportedException()
            );
    }
}
