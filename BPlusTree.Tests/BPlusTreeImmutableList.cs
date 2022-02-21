using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    using static Storage;

    public sealed partial class BPlusTreeImmutableList<T>
    {
        private readonly Node _root;
        private readonly int _count;

        private BPlusTreeImmutableList(Node root, int count)
        {
            Debug.Assert(count >= 0);

            this._root = root;
            this._count = count;
        }

        public int Count => this._count;

        public T this[int index] => this.ItemRef(index);

        public ref readonly T ItemRef(int index)
        {
            if ((uint)index < (uint)this._count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            Node current = this._root;
            while (true)
            {
                start:
                if (current is InternalNode internalNode)
                {
                    var lastChildIndex = internalNode.ChildrenCount - 1;
                    for (var i = 0; i < lastChildIndex; ++i)
                    {
                        int childCount = Get(ref internalNode.ChildrenCounts, i);
                        if (index < childCount)
                        {
                            current = Get(ref internalNode.Children, i);
                            goto start;
                        }

                        index -= childCount;
                    }

                    current = Get(ref internalNode.Children, lastChildIndex);
                }
                else // leaf
                {
                    Debug.Assert(index < NodeSize);
                    return ref Get(ref ((LeafNode)current).Items, index);
                }
            }
        }
    }
}
