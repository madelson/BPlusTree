using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    [DebuggerDisplay("CumulativeChildCount = {CumulativeChildCount}")]
    internal struct ArrayBasedBPlusTreeImmutableListInternalEntry
    {
        private const int CumulativeChildCountMask = int.MaxValue;
        private const int IsChildMutableMask = ~int.MaxValue;

        public int CumulativeChildCount;
        public Array Child;

        public static ArrayBasedBPlusTreeImmutableListInternalEntry CreateMutable(Array child, int cumulativeChildCount)
        {
            Debug.Assert((cumulativeChildCount & IsChildMutableMask) == 0);
            return new() { Child = child, CumulativeChildCount = cumulativeChildCount | IsChildMutableMask };
        }

        public int CumulativeChildCountForBuilder
        {
            get => CumulativeChildCount & CumulativeChildCountMask;
            set
            {
                Debug.Assert((value & IsChildMutableMask) == 0);
                CumulativeChildCount = (CumulativeChildCount & IsChildMutableMask) | value;
            }
        }

        public bool IsChildMutable
        {
            get => (CumulativeChildCount & IsChildMutableMask) != 0;
            set => CumulativeChildCount = CumulativeChildCountForBuilder | (value ? IsChildMutableMask : 0);
        }

        public override string ToString() => 
            $"{nameof(CumulativeChildCount)} = {CumulativeChildCountForBuilder}, {nameof(Child)} = {Child?.Length} items{(IsChildMutable ? " (MUTABLE)" : string.Empty)}";
    }
}
