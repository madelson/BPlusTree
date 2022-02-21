//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace BPlusTree.Tests
//{
//    public sealed partial class BTreeImmutableList<T>
//    {
//        internal abstract class Node
//        {
//            public const int Size = 8;
//            public const int SplitSize = Size / 2;

//            public abstract ref readonly T ItemRef(int index, int count);

//            public abstract (Node Node, Node? Split) Insert(int index, T item, int count);
//        }

//        internal sealed class InternalNode : Node
//        {
//            private int _childCount;
//            private Storage7<int> _cumulativeCounts;
//            private Storage8<Node?> _children;

//            public override ref readonly T ItemRef(int index, int count)
//            {
//                Debug.Assert(index >= 0 && index < count);

//                Span<int> cumulativeCounts = Storage.CreateSpan(ref this._cumulativeCounts, this._childCount - 1);
//                int childIndex = cumulativeCounts.BinarySearch(index);
//                if (childIndex >= 0)
//                {
//                    ++childIndex;
//                }
//                else
//                {
//                    childIndex = ~childIndex;
//                }

//                int previousCumulativeCount = childIndex > 0 ? cumulativeCounts[childIndex - 1] : 0;
//                int countWithinChild = (childIndex == Size - 1 ? count : cumulativeCounts[childIndex]) - previousCumulativeCount;
//                int indexWithinChild = index - previousCumulativeCount;
//                return ref Storage.CreateSpan(ref this._children)[childIndex]!.ItemRef(indexWithinChild, countWithinChild);
//            }

//            public override (Node Node, Node? Split) Insert(int index, T item, int count)
//            {
//                Debug.Assert(index >= 0 && index < count);

//                Span<int> cumulativeCounts = Storage.CreateSpan(ref this._cumulativeCounts, this._childCount - 1);
//                int childIndex = cumulativeCounts.BinarySearch(index);
//                if (childIndex >= 0)
//                {
//                    ++childIndex;
//                }
//                else
//                {
//                    childIndex = ~childIndex;
//                }

//                Span<Node?> children = Storage.CreateSpan(ref this._children);
//                int previousCumulativeCount = childIndex > 0 ? cumulativeCounts[childIndex - 1] : 0;
//                int countWithinChild = (childIndex == Size - 1 ? count : cumulativeCounts[childIndex]) - previousCumulativeCount;
//                int indexWithinChild = index - previousCumulativeCount;
//                (Node newChild, Node? split) = children[childIndex]!.Insert(indexWithinChild, item, countWithinChild);

//                if (split is null)
//                {

//                }
//            }
//        }

//        internal sealed class LeafNode : Node
//        {
//            private Storage8<T> _values;

//            public override ref readonly T ItemRef(int index, int count)
//            {
//                Debug.Assert(index >= 0 && index < count && index < Size);

//                return ref Storage.CreateSpan(ref this._values)[index];
//            }

//            public override (Node Node, Node? Split) Insert(int index, T item, int count)
//            {
//                Debug.Assert(index >= 0 && index < count && index < Size);

//                Span<T> originalValues = Storage.CreateSpan(ref this._values, count);

//                if (count < Size)
//                {
//                    LeafNode copy = new();
//                    Span<T> newValues = Storage.CreateSpan(ref copy._values, count + 1);
//                    originalValues.Slice(0, index).CopyTo(newValues);
//                    newValues[index] = item;
//                    originalValues.Slice(index, count - index).CopyTo(newValues.Slice(index + 1));
//                    return (copy, null);
//                }

//                LeafNode leftSplit = new(), rightSplit = new();
//                Span<T> leftSplitValues = Storage.CreateSpan(ref leftSplit._values, SplitSize),
//                    rightSplitValues = Storage.CreateSpan(ref rightSplit._values, SplitSize);
//                int newValueOffset = 0;
//                for (var i = 0; i < Size; ++i)
//                {
//                    if (i == index)
//                    {
//                        newValueOffset = 1;
//                        if (i < SplitSize)
//                        {
//                            leftSplitValues[i] = item;
//                        }
//                        else
//                        {
//                            rightSplitValues[i - SplitSize] = item;
//                        }
//                    }
//                    else
//                    {
//                        if (i < SplitSize)
//                        {
//                            leftSplitValues[i] = originalValues[i - newValueOffset];
//                        }
//                        else
//                        {
//                            rightSplitValues[i - 4] = originalValues[i - newValueOffset];
//                        }
//                    }
//                }
//                return (leftSplit, rightSplit);
//            }
//        }
//    }
//}
