using System.Runtime.InteropServices;
using Xunit;

namespace BPlusTree.Tests
{
    public class StorageTests
    {
        [Fact]
        public void TestStorage7()
        {
            var storage7 = default(Storage7<WeirdLayout>);
            storage7.Item2.Object = this;
            storage7.Item3.Byte = 12;
            storage7.Item5.Int16 = -1;
            storage7.Item7.String = "abc";

            var span = Storage.CreateSpan(ref storage7);
            Assert.Equal(7, span.Length);
            Assert.Equal(this, span[1].Object);
            Assert.Equal(12, span[2].Byte);
            Assert.Equal(-1, span[4].Int16);
            Assert.Equal("abc", span[6].String);
        }

        [Fact]
        public void TestStorage8()
        {
            var storage8 = default(Storage8<WeirdLayout>);
            storage8.Items1To7.Item1.Object = "test";
            storage8.Items1To7.Item6.Byte = byte.MaxValue;
            storage8.Items1To7.Item7.Int16 = short.MaxValue;
            storage8.Item8.String = "end";

            var span = Storage.CreateSpan(ref storage8);
            Assert.Equal(8, span.Length);
            Assert.Equal("test", span[0].Object);
            Assert.Equal(byte.MaxValue, span[5].Byte);
            Assert.Equal(short.MaxValue, span[6].Int16);
            Assert.Equal("end", span[7].String);
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