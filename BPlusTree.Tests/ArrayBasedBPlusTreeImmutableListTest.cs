using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
