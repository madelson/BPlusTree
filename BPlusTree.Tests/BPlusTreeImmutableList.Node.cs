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
            internal abstract (Node Updated, Node? Split, int UpdatedCount) Insert(T item, int index, int count);
        }
    }
}
