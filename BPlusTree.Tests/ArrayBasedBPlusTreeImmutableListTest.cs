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
            var list = ArrayBasedBPlusTreeImmutableList<string>.CreateRange(strings);
            Assert.AreEqual(strings.Length, list.Count);
            for (var i = 0; i < strings.Length; ++i)
            {
                Assert.AreEqual(strings[i], list[i]);
            }

            var largeRange = Enumerable.Range(0, 1000).Select(i => -i * i).ToArray();
            var largeList = ArrayBasedBPlusTreeImmutableList<int>.CreateRange(largeRange);
            Assert.AreEqual(largeRange.Length, largeList.Count);
            for (var i = 0; i < largeRange.Length; ++i)
            {
                Assert.AreEqual(largeRange[i], largeList[i]);
            }
        }

        [Test]
        public void TestEmpty()
        {
            ArrayBasedBPlusTreeImmutableList<int> empty = ArrayBasedBPlusTreeImmutableList<int>.Empty;
            Assert.AreEqual(0, empty.Count);
            Assert.IsEmpty(empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => empty[0].ToString());

            Assert.AreSame(ArrayBasedBPlusTreeImmutableList<char>.Empty, ArrayBasedBPlusTreeImmutableList<char>.CreateRange(Enumerable.Empty<char>().ToArray()));
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
        public void TestCreateRange([Values(5, 50, 512, 10_000)] int count)
        {
            var items = Enumerable.Range(0, count).ToArray();
            var list = ArrayBasedBPlusTreeImmutableList<int>.CreateRange(items);

            Assert.AreEqual(count, list.Count);
            for (var i = 0; i < count; ++i)
            {
                Assert.AreEqual(items[i], list[i]);
            }
        }
    }
}
