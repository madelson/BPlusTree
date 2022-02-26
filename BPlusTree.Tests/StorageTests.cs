using System.Runtime.InteropServices;
using NUnit.Framework;

namespace BPlusTree.Tests
{
    using static Storage;

    public class StorageTests
    {
        [Test]
        public void TestStorage7()
        {
            var storage7 = default(Storage7<WeirdLayout>);
            storage7.Item2.Object = this;
            storage7.Item3.Byte = 12;
            storage7.Item5.Int16 = -1;
            storage7.Item7.String = "abc";

            Assert.AreEqual(this, Get(ref storage7, 1).Object);
            Assert.AreEqual(12, Get(ref storage7, 2).Byte);
            Assert.AreEqual(-1, Get(ref storage7, 4).Int16);
            Assert.AreEqual("abc", Get(ref storage7, 6).String);
        }

        [Test]
        public void TestStorage8()
        {
            var storage8 = default(Storage8<WeirdLayout>);
            storage8.Items1To7.Item1.Object = "test";
            storage8.Items1To7.Item6.Byte = byte.MaxValue;
            storage8.Items1To7.Item7.Int16 = short.MaxValue;
            storage8.Item8.String = "end";

            Assert.AreEqual("test", Get(ref storage8, 0).Object);
            Assert.AreEqual(byte.MaxValue, Get(ref storage8, 5).Byte);
            Assert.AreEqual(short.MaxValue, Get(ref storage8, 6).Int16);
            Assert.AreEqual("end", Get(ref storage8, 7).String);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WeirdLayout
        {
            internal object? Object;
            internal byte Byte;
            internal short Int16;
            internal string? String;
        }
    }
}