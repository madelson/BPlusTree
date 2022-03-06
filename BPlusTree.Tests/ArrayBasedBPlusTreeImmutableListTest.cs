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
