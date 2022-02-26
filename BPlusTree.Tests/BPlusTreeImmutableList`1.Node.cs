using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    public partial class BPlusTreeImmutableList<T>
    {
        internal abstract class Node
        {
            internal abstract int Count { get; }

            internal abstract (Node Updated, Node? Split) Insert(int index, T item);
        }
    }
}
