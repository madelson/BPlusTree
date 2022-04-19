using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    public class ArrayBasedBPlusTreeImmutableListTest
    {
        [Test]
        public void TestIndexing()
        {
            var strings = new[] { "m", "e", "d", "a", "l", "l", "i", "o", "n" };
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(strings);
            Assert.AreEqual(strings.Length, list.Count);
            for (var i = 0; i < strings.Length; ++i)
            {
                Assert.AreEqual(strings[i], list[i]);
            }

            var largeRange = Enumerable.Range(0, 1000).Select(i => -i * i).ToArray();
            var largeList = ArrayBasedBPlusTreeImmutableList.CreateRange(largeRange);
            Assert.AreEqual(largeRange.Length, largeList.Count);
            for (var i = 0; i < largeRange.Length; ++i)
            {
                Assert.AreEqual(largeRange[i], largeList[i]);
            }
        }

        [Test]
        public void TestSetItem()
        {
            ArrayBasedBPlusTreeImmutableList<int> list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 5000));
            for (var i = 0; i < list.Count; ++i)
            {
                list = list.SetItem(i, list[i] * list[i]);
            }

            CollectionAssert.AreEqual(Enumerable.Range(0, 5000).Select(i => i * i), list);
        }

        [Test]
        public void TestEmpty()
        {
            ArrayBasedBPlusTreeImmutableList<int> empty = ArrayBasedBPlusTreeImmutableList<int>.Empty;
            Assert.AreEqual(0, empty.Count);
            Assert.IsEmpty(empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => empty[0].ToString());
            Assert.Throws<ArgumentOutOfRangeException>(() => empty.Insert(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => empty.Insert(1, 0));

            Assert.AreSame(ArrayBasedBPlusTreeImmutableList<char>.Empty, ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Empty<char>().ToArray()));
            Assert.AreSame(empty, empty.AddRange(empty));
        }

        [Test]
        public void TestInsert()
        {
            ArrayBasedBPlusTreeImmutableList<string> list = ArrayBasedBPlusTreeImmutableList<string>.Empty;

            list = list.Insert(0, "a");
            CollectionAssert.AreEqual(new[] { "a" }, list);

            list = list.Insert(1, "b").Insert(0, "c");
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, list);

            Assert.IsEmpty(ArrayBasedBPlusTreeImmutableList<string>.Empty);

            for (var i = 99; i >= 0; --i)
            {
                list = list.Insert(3, $"x{i}");
            }
            for (var i = 99; i >= 0; --i)
            {
                list = list.Insert(2, $"y{i}");
            }
            for (var i = 99; i >= 0; --i)
            {
                list = list.Insert(1, $"z{i}");
            }
            CollectionAssert.AreEquivalent(
                new[] { "c" }
                    .Concat(Enumerable.Range(0, 100).Select(i => $"x{i}"))
                    .Concat(new[] { "a" })
                    .Concat(Enumerable.Range(0, 100).Select(i => $"y{i}"))
                    .Concat(new[] { "b" })
                    .Concat(Enumerable.Range(0, 100).Select(i => $"z{i}")),
                list
            );
        }

        [Test]
        public void TestCreateRange([Values(1, 5, 10, 20, 40, 80, 10000)] int count)
        {
            string[] values = Enumerable.Range(0, count).Select(i => i.ToString()).ToArray();
            ArrayBasedBPlusTreeImmutableList<string> list = ArrayBasedBPlusTreeImmutableList.CreateRange(values);
            CollectionAssert.AreEqual(values, list);
        }

        [Test]
        public void TestAddRangeWithEmptyList()
        {
            ArrayBasedBPlusTreeImmutableList<bool?> empty = ArrayBasedBPlusTreeImmutableList<bool?>.Empty;
            Assert.AreSame(empty, empty.AddRange(Enumerable.Empty<bool?>()));

            ArrayBasedBPlusTreeImmutableList<bool?> list = ArrayBasedBPlusTreeImmutableList.CreateRange(new bool?[] { null, false, true });
            Assert.AreSame(list, list.AddRange(empty));
            Assert.AreSame(list, empty.AddRange(list));
        }

        [Test, Combinatorial]
        public void TestAddRange(
            [Values(1, 7, 49, 123, 10000)] int startCount,
            [Values(1, 7, 49, 123, 10000)] int addCount)
        {
            string[] startingValues = Enumerable.Range(0, startCount).Select(i => $"s{i}").ToArray();
            string[] addedValues = Enumerable.Range(0, addCount).Select(i => $"a{i}").ToArray();
            ArrayBasedBPlusTreeImmutableList<string> list = ArrayBasedBPlusTreeImmutableList.CreateRange(startingValues);
            list = list.AddRange(addedValues);
            CollectionAssert.AreEqual(startingValues.Concat(addedValues), list);
        }

        [Test]
        public void TestAdd()
        {
            ArrayBasedBPlusTreeImmutableList<char> list = ArrayBasedBPlusTreeImmutableList<char>.Empty;
            list = list.Add('x').Add('y').Add('z');
            CollectionAssert.AreEqual("xyz", list);
            for (var i = 0; i < 100; ++i)
            {
                list = list.Add('m');
            }
            CollectionAssert.AreEqual("xyz" + new string('m', 100), list);
        }

        [Test, Combinatorial]
        public void TestAddMany(
            [Values(1, 7, 49, 123, 10000)] int startCount,
            [Values(1, 7, 49, 123, 10000)] int addCount)
        {
            string[] startingValues = Enumerable.Range(0, startCount).Select(i => $"s{i}").ToArray();
            string[] addedValues = Enumerable.Range(0, addCount).Select(i => $"a{i}").ToArray();
            ArrayBasedBPlusTreeImmutableList<string> list = startingValues.Aggregate(ArrayBasedBPlusTreeImmutableList<string>.Empty, (l, s) => l.Add(s));
            foreach (string value in addedValues)
            {
                list = list.Add(value);
            }
            CollectionAssert.AreEqual(startingValues.Concat(addedValues), list);
        }

        [Test]
        public void TestRemoveAt()
        {
            const int Count = 500;
            ArrayBasedBPlusTreeImmutableList<int> list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, Count));
            for (var i = list.Count - 1; i >= 0; i -= 2)
            {
                list = list.RemoveAt(i);
            }
            CollectionAssert.AreEqual(Enumerable.Range(0, Count / 2).Select(i => 2 * i), list);
        }

        [Test]
        public void TestRemoveAtLast()
        {
            ArrayBasedBPlusTreeImmutableList<int> list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 33));
            CollectionAssert.AreEqual(Enumerable.Range(0, 32), list.RemoveAt(32));
        }

        [Test]
        public void TestRemoveAtEdgeCase()
        {
            ArrayBasedBPlusTreeImmutableList<int> list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 10_000));

            var random = new Random(12345);
            var removes = new List<int>();
            for (var i = list.Count; i > 0; --i)
            {
                removes.Add(random.Next(i));
            }

            foreach (var index in removes)
            {
                list = list.RemoveAt(index);
            }

            Assert.AreSame(ArrayBasedBPlusTreeImmutableList<int>.Empty, list);
        }

        [Test]
        public void TestRemoveRange()
        {
            ArrayBasedBPlusTreeImmutableList<int> list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 1000));
            
            CollectionAssert.AreEqual(new[] { 0, 999 }, list.RemoveRange(1, 998));

            CollectionAssert.AreEqual(Enumerable.Range(500, 500), list.RemoveRange(0, 500));

            CollectionAssert.AreEqual(Enumerable.Range(0, 500), list.RemoveRange(500, 500));

            CollectionAssert.AreEqual(Enumerable.Range(0, 237).Concat(Enumerable.Range(387, 1000 - 387)), list.RemoveRange(237, 150));

            list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 658));
            CollectionAssert.AreEqual(Enumerable.Range(0, 481).Concat(Enumerable.Range(481 + 133, 658 - 481 - 133)), list.RemoveRange(481, 133));
        }

        [Test]
        public void TestBuilderSetItem()
        {
            ArrayBasedBPlusTreeImmutableList<string> list = ArrayBasedBPlusTreeImmutableList.CreateRange(new[] { "a", "b" });
            ArrayBasedBPlusTreeImmutableList<string>.Builder builder = list.ToBuilder();
            Assert.AreEqual(list.Count, builder.Count);
            CollectionAssert.AreEqual(list, builder);
            CollectionAssert.AreEqual(list, builder.ToImmutable());

            builder[1] = "c";
            Assert.AreEqual("c", builder[1]);
            CollectionAssert.AreNotEqual(list, builder);
            CollectionAssert.AreNotEqual(list, builder.ToImmutable());
            CollectionAssert.AreEqual(list.SetItem(1, "c"), builder);
            CollectionAssert.AreEqual(list.SetItem(1, "c"), builder.ToImmutable());
        }

        [Test]
        public void TestIndexOf()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(
                Enumerable.Range(0, 32).Concat(Enumerable.Range(0, 32)).Select(i => i.ToString()));

            Assert.AreEqual(0, list.IndexOf("0"));
            Assert.AreEqual(-1, list.IndexOf("x"));
            Assert.AreEqual(33, list.IndexOf("1", startIndex: 5));
            Assert.AreEqual(-1, list.IndexOf("19", startIndex: list.Count - 5));
            Assert.AreEqual(5, list.IndexOf("5", startIndex: 2, count: 4));
            Assert.AreEqual(-1, list.IndexOf("30", startIndex: 20, count: 9));
        }

        [Test]
        public void TestCopyTo()
        {
            var sequence = Enumerable.Range(0, 100).Reverse().ToArray();
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(sequence);
            var array = new int[sequence.Length + 1];
            list.CopyTo(array, 1);
            CollectionAssert.AreEqual(new[] { 0 }.Concat(sequence), array);
        }

        [Test]
        public void TestContains()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange("abc");
            Assert.True(list.Contains('b'));
            Assert.False(list.Contains('d'));
        }

        [Test]
        public void TestEnumerator()
        {
            CollectionAssert.AreEqual(Enumerable.Empty<string>(), ArrayBasedBPlusTreeImmutableList<string>.Empty.Select(i => i + i));

            var sequence = Enumerable.Range(0, 20).OrderBy(i => i % 4);
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(sequence);
            CollectionAssert.AreEqual(sequence, list.Select(i => i));

            sequence = Enumerable.Range(0, 12345).OrderByDescending(i => i % 177);
            list = ArrayBasedBPlusTreeImmutableList.CreateRange(sequence);
            CollectionAssert.AreEqual(sequence, list.Select(i => i));
        }

        [Test]
        public void TestEnumeratorEdgeCases()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(1, 100).Select(i => i.ToString()));
            using var enumerator = list.GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => { var _ = enumerator.Current; });

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("1", enumerator.Current);

            while (enumerator.MoveNext()) ;
            Assert.Throws<InvalidOperationException>(() => { var _ = enumerator.Current; });
            Assert.IsFalse(enumerator.MoveNext());

            enumerator.Dispose();
            Assert.Throws<ObjectDisposedException>(() => { var _ = enumerator.Current; });
            Assert.Throws<ObjectDisposedException>(() => enumerator.MoveNext());
            Assert.Throws<ObjectDisposedException>(() => enumerator.Reset());
        }

        [Test]
        public void TestEnumeratorReset()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(1, 1000).Select(i => i.ToString()));
            using var enumerator = list.GetEnumerator();

            for (var i = 0; i < 503; ++i) { Assert.IsTrue(enumerator.MoveNext()); }
            Assert.AreEqual("503", enumerator.Current);

            enumerator.Reset();
            Assert.Throws<InvalidOperationException>(() => { var _ = enumerator.Current; });

            for (var i = 0; i < 797; ++i) { Assert.IsTrue(enumerator.MoveNext()); }
            Assert.AreEqual("797", enumerator.Current);
        }

        [Test]
        public void TestFind()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 1000).Select(i => (i * i).ToString()));
            Assert.IsNull(list.Find(s => s.Length == 0));
            Assert.AreEqual("49", list.Find(s => s.Length == 2 && s.StartsWith("4")));
            Assert.AreEqual(list.ToList().FindIndex(100, 300, s => s[1] == s[2]), list.FindIndex(100, 300, s => s[1] == s[2]));
        }

        [Test]
        public void TestConvertAll()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 1000));

            CollectionAssert.AreEqual(
                list.Select(i => i * 2),
                list.ConvertAll(i => i * 2)
            );

            CollectionAssert.AreEqual(
                list.Select(i => (i / 10).ToString()),
                list.ConvertAll(i => (i / 10).ToString())
            );
        }

        [Test]
        public void TestForEach()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 333));
            var sum = 0;
            list.ForEach(i => sum += i);
            Assert.AreEqual(Enumerable.Range(0, 333).Sum(), sum);
        }

#if NET
        [Test]
        public void TestDoesNotAllocate()
        {
            var list = ArrayBasedBPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 1000));

            AssertDoesNotAllocate(() => list.IndexOf(900));
            AssertDoesNotAllocate(() => list.Contains(800));
            AssertDoesNotAllocate(() => list.Find(i => i == 500));
            AssertDoesNotAllocate(() => list.FindIndex(25, 750, i => i % 237 == 0));
            AssertDoesNotAllocate(() => list.ForEach(_ => { }));
            var array = new int[list.Count];
            AssertDoesNotAllocate(() => list.CopyTo(array, 0));
            var counter = 0;
            AssertDoesNotAllocate(() =>
            {
                foreach (var item in list) { ++counter; }
            });
        }

        private static void AssertDoesNotAllocate(Action action)
        {
            GetBytes(); // warmup
            Assert.AreEqual(0, GetBytes());

            [MethodImpl(MethodImplOptions.NoInlining)]
            long GetBytes()
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                action();
                return GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
        }
#endif
    }
}
