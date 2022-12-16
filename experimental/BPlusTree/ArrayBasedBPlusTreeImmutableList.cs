using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    public static class ArrayBasedBPlusTreeImmutableList
    {
        public static ArrayBasedBPlusTreeImmutableList<T> CreateRange<T>(IEnumerable<T> items) =>
            ArrayBasedBPlusTreeImmutableList<T>.Empty.AddRange(items);
    }
}
