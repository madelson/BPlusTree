using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    public class BPlusTreeImmutableListTest
    {
        [Test]
        public void TestEmpty()
        {
            BPlusTreeImmutableList<int> empty = BPlusTreeImmutableList<int>.Empty;
            Assert.AreEqual(0, empty.Count);
            Assert.IsEmpty(empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => empty[0].ToString());

            Assert.AreSame(BPlusTreeImmutableList<char>.Empty, BPlusTreeImmutableList.CreateRange(Enumerable.Empty<char>()));
        }

        [Test]
        public void TestInsert()
        {
            BPlusTreeImmutableList<string> list = BPlusTreeImmutableList<string>.Empty;
            
            list = list.Insert(0, "a");
            CollectionAssert.AreEqual(new[] { "a" }, list);

            list = list.Insert(1, "b").Insert(0, "c");
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, list);

            Assert.IsEmpty(BPlusTreeImmutableList<string>.Empty);

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
            BPlusTreeImmutableList<string> list = BPlusTreeImmutableList.CreateRange(values);
            CollectionAssert.AreEqual(values, list);
        }

        [Test]
        public void TestAddRangeWithEmptyList()
        {
            BPlusTreeImmutableList<bool?> empty = BPlusTreeImmutableList<bool?>.Empty;
            Assert.AreSame(empty, empty.AddRange(Enumerable.Empty<bool?>()));

            BPlusTreeImmutableList<bool?> list = BPlusTreeImmutableList.CreateRange(new bool?[] { null, false, true });
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
            BPlusTreeImmutableList<string> list = BPlusTreeImmutableList.CreateRange(startingValues);
            list = list.AddRange(addedValues);
            CollectionAssert.AreEqual(startingValues.Concat(addedValues), list);
        }
    }
}
