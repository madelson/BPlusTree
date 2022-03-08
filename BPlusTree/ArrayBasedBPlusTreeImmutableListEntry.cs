using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    internal struct ArrayBasedBPlusTreeImmutableListInternalEntry
    {
        public int CumulativeChildCount;
        public Array Child;

        public override string ToString() => $"{nameof(CumulativeChildCount)} = {CumulativeChildCount}, {nameof(Child)} = {Child?.Length} items";
    }
}
