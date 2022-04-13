using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    internal class ArrayBasedBPlusTreeImmutableListCompatTest
    {
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
