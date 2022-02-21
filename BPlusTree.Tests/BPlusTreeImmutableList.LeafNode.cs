using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    using static Storage;

    public partial class BPlusTreeImmutableList<T>
    {
        internal sealed class LeafNode : Node
        {
            internal Storage8<T> Items;

            internal override (Node Updated, Node? Split, int UpdatedCount) Insert(T item, int index, int count)
            {
                Debug.Assert(count is >= 0 and <= NodeSize);
                Debug.Assert(index >= 0 && index < count);

                if (count < NodeSize)
                {
                    LeafNode updated = new();
                    for (var i = 0; i < index; ++i)
                    {
                        Get(ref updated.Items, i) = Get(ref this.Items, i);
                    }
                    Get(ref updated.Items, index) = item;
                    for (int i = index; i < count; ++i)
                    {
                        Get(ref updated.Items, i + 1) = Get(ref this.Items, i);
                    }
                    return (updated, null, count + 1);
                }

                LeafNode left = new(), right = new();
                int insertionOffset = 0;
                for (var i = 0; i < NodeSize; ++i)
                {
                    if (i == index)
                    {
                        insertionOffset = 1;
                        if (i < SplitNodeSize)
                        {
                            Get(ref left.Items, i) = item;
                        }
                        else
                        {
                            Get(ref right.Items, i - SplitNodeSize) = item;
                        }
                    }
                    else if (i < SplitNodeSize)
                    {
                        Get(ref left.Items, i) = Get(ref this.Items, i - insertionOffset);
                    }
                    else
                    {
                        Get(ref right.Items, i - SplitNodeSize) = Get(ref this.Items, i - insertionOffset);
                    }
                }
                return (left, right, SplitNodeSize);
            }
        }
    }
}
