using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    internal class ArrayBasedBPlusTreeImmutableListCompatTest
    {
        [Test]
        public void TestListApiCompat() => TestApiCompat(typeof(ArrayBasedBPlusTreeImmutableList<>), typeof(ImmutableList<>));

        [Test]
        public void TestBuilderApiCompat() => TestApiCompat(typeof(ArrayBasedBPlusTreeImmutableList<>.Builder), typeof(ImmutableList<>.Builder));

        [Test]
        public void TestEnumeratorApiCompat() => TestApiCompat(typeof(ArrayBasedBPlusTreeImmutableList<>.Enumerator), typeof(ImmutableList<>.Enumerator));

        [Test]
        public void TestStaticAccessApiCompat() => TestApiCompat(typeof(ArrayBasedBPlusTreeImmutableList), typeof(ImmutableList));

        [Test]
        public void TestAddCompat() =>
            TestCompat((l, r) => l.Add(r.Next()));

        [Test]
        public void TestAddRangeCompat() =>
            TestCompat((l, r) => l.AddRange(Enumerable.Range(r.Next(20), r.Next(20))));

        [Test]
        public void TestInsertCompat() =>
            TestCompat((l, r) => l.Insert(index: r.Next(l.Count + 1), element: r.Next()));

        [Test]
        public void TestSetItemCompat() =>
            TestCompat((l, r) => l.SetItem(r.Next(l.Count), r.Next()));

        [Test]
        public void TestBuilderSetItemCompat() =>
            TestCompat((l, r) =>
            {
                IList<int> builder = ToBuilder(l);
                var modifyCount = r.Next(builder.Count + 1);
                for (var i = 0; i < modifyCount; ++i)
                {
                    builder[r.Next(builder.Count)] = r.Next();
                }
                return ToImmutable(builder);
            });

        [Test]
        public void TestBuilderAddCompat() =>
            TestCompat((l, r) =>
            {
                IList<int> builder = ToBuilder(l);
                var addCount = r.Next(10);
                for (var i = 0; i < addCount; ++i)
                {
                    builder.Add(r.Next());
                }
                return ToImmutable(builder);
            });

        [Test]
        public void TestRemoveCompat() =>
            TestCompat((l, r) => l.Remove(l[r.Next(l.Count)]));

        [Test]
        public void TestRemoveAllCompat() =>
            TestCompat((l, r) => 
            {
                var index = r.Next(l.Count);
                return l.RemoveAll(i => i == l[index] || i == l[index / 2]);
            });

        [Test]
        public void TestRemoveAtCompat() =>
            TestCompat((l, r) => l.RemoveAt(r.Next(l.Count)));

        [Test]
        public void TestRemoveRangeCompat() =>
            TestCompat((l, r) =>
            {
                if (l.Count == 0)
                {
                    l = l.AddRange(Enumerable.Range(r.Next(10000), r.Next(100, 1000)));
                }
                var index = r.Next(l.Count);
                return l.RemoveRange(index, r.Next(l.Count - index + 1));
            });

        [Test]
        public void TestRemoveRangeEnumerableCompat() =>
            TestCompat((l, r) =>
            {
                if (l.Count == 0)
                {
                    l = l.AddRange(Enumerable.Range(r.Next(100), r.Next(10, 100)));
                }
                var toRemove = l.Where(_ => r.NextDouble() < .1);
                return l.RemoveRange(toRemove);
            });

        [Test]
        public void TestReverseCompat() =>
            TestCompat((l, r) =>
            {
                var index = r.Next(l.Count);
                var count = r.Next(l.Count - index + 1);
                return l switch
                {
                    ImmutableList<int> immutableList => immutableList.Reverse(index, count),
                    ArrayBasedBPlusTreeImmutableList<int> arrayBasedList => arrayBasedList.Reverse(index, count),
                    _ => throw new NotSupportedException(l.GetType().FullName)
                };
            });

        [Test]
        public void TestGetRangeCompat() =>
            TestCompat((l, r) =>
            {
                if (l.Count == 0)
                {
                    l = l.AddRange(Enumerable.Range(r.Next(10000), r.Next(100, 1000)));
                }
                var index = r.Next(l.Count);
                var count = r.Next(l.Count - index + 1);
                return l is ArrayBasedBPlusTreeImmutableList<int> a ? a.GetRange(index, count) : ((ImmutableList<int>)l).GetRange(index, count);
            });

        [Test]
        public void TestReplaceCompat() =>
            TestCompat((l, r) => l.Replace(l[r.Next(l.Count)], r.Next(100)));

        [Test]
        public void TestConvertAllCompat() =>
            TestCompat((l, r) => l is ArrayBasedBPlusTreeImmutableList<int> a ? a.ConvertAll(i => i + r.Next(10)) : ((ImmutableList<int>)l).ConvertAll(i => i + r.Next(10)));

        private static void TestApiCompat(Type actual, Type expected)
        {
            AssertEqual(GetInterfaces(expected), GetInterfaces(actual));
            AssertEqual(GetFields(expected), GetFields(actual));
            AssertEqual(GetMethods(expected), GetMethods(actual));

            static void AssertEqual(IEnumerable<string> expected, IEnumerable<string> actual) => CollectionAssert.AreEqual(
                expected,
                actual,
                $"EXPECTED:{Environment.NewLine}{string.Join(Environment.NewLine, expected)}{Environment.NewLine}{Environment.NewLine}ACTUAL:{Environment.NewLine}{string.Join(Environment.NewLine, actual)}");

            static IEnumerable<string> GetInterfaces(Type type) => type.GetInterfaces().Select(i => i.Name).OrderBy(s => s);

            static IEnumerable<string> GetFields(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Select(f => f.ToString()!.Replace(type.FullName!, "IMMUTABLELIST"))
                .OrderBy(s => s);

            static IEnumerable<string> GetMethods(Type type) =>
                type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Select(m => m.ToString()!.Replace(type.FullName!, "IMMUTABLELIST"))
                    .OrderBy(s => s);
        }

        private static void TestCompat(Func<IImmutableList<int>, Random, IImmutableList<int>> transform)
        {
            var sizes = new[] { 5, 50, 500 };
            foreach (var size in sizes)
            {
                var baselines = new List<IImmutableList<int>>();
                baselines.Add(ImmutableList.CreateRange(Enumerable.Range(100, size)));

                var results = new List<IImmutableList<int>>();
                results.Add(ArrayBasedBPlusTreeImmutableList.CreateRange(baselines[0]));

                Test(baselines);
                Test(results);

                for (var i = 0; i < baselines.Count; ++i)
                {
                    CollectionAssert.AreEqual(baselines[i], results[i], $"i={i}, size={size}");
                    // TODO add this test; requires tracking the original immutable list in the builder
                    //if (i > 0 && baselines[i] == baselines[i - 1])
                    //{
                    //    Assert.AreSame(results[i - 1], results[i]);
                    //}
                }

                void Test(List<IImmutableList<int>> results)
                {
                    var random = new Random(12345);
                    for (var i = 0; i < size / 3; ++i)
                    {
                        results.Add(transform(results[results.Count - 1], random));
                    }
                }
            }
        }

        private static IList<T> ToBuilder<T>(IImmutableList<T> list) =>
            list switch
            {
                ImmutableList<T> immutableList => immutableList.ToBuilder(),
                ArrayBasedBPlusTreeImmutableList<T> arrayBasedList => arrayBasedList.ToBuilder(),
                _ => throw new NotSupportedException(list.GetType().FullName),
            };

        private static IImmutableList<T> ToImmutable<T>(IList<T> builder) =>
            builder switch
            {
                ImmutableList<T>.Builder immutableListBuilder => immutableListBuilder.ToImmutable(),
                ArrayBasedBPlusTreeImmutableList<T>.Builder arrayBasedBuilder => arrayBasedBuilder.ToImmutable(),
                _ => throw new NotSupportedException(builder.GetType().FullName),
            };
    }
}
