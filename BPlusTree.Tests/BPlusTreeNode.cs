//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Diagnostics.CodeAnalysis;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace BPlusTree.Tests
//{
//    internal abstract class BPlusTreeNode<TKey, TValue>
//    {
//        protected BPlusTreeNode(BPlusTreeNode<TKey, TValue> node, int countDelta)
//        {
//            this._countAndFrozenFlag = node._countAndFrozenFlag + countDelta;
//        }

//        protected BPlusTreeNode(int count, bool isFrozen)
//        {
//            Debug.Assert(count is > 0 and <= 8);
//            this._countAndFrozenFlag = count | (isFrozen ? FrozenFlag : 0);
//        }

//        private const int FrozenFlag = 1 << 9;

//        private int _countAndFrozenFlag;

//        public int Count => (byte)_countAndFrozenFlag;

//        public void ChangeCount(int delta)
//        {
//            Debug.Assert(!this.IsFrozen);
//            Debug.Assert(this.Count + delta is > 0 and <= 8);
//            this._countAndFrozenFlag += delta;
//        }

//        public void Freeze() => this._countAndFrozenFlag |= FrozenFlag;

//        public bool IsFrozen => (this._countAndFrozenFlag & FrozenFlag) != 0;

//        internal abstract bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);

//        internal abstract BPlusTreeNode<TKey, TValue> Add(TKey key, TValue value, out BPlusTreeNode<TKey, TValue>? split, out TKey? splitKey);
//    }

//    internal sealed class BPlusTreeLeafNode<TKey, TValue> : BPlusTreeNode<TKey, TValue>
//    {
//        internal Storage8<Entry> Entries;

//        private BPlusTreeLeafNode(BPlusTreeLeafNode<TKey, TValue> node, int countDelta)
//            : base(node, countDelta)
//        {
//            this.Entries = node.Entries;
//        }

//        private BPlusTreeLeafNode(int count, bool isFrozen) : base(count, isFrozen) { }

//        internal override BPlusTreeNode<TKey, TValue> Add(TKey key, TValue value, out BPlusTreeNode<TKey, TValue>? split, out TKey? splitKey)
//        {
//            Span<Entry> storageSpan = Storage.CreateSpan(ref this.Entries);
//            int count = this.Count;
//            int index = storageSpan.Slice(0, count).BinarySearch(new EntryComparable(key));
//            if (index >= 0)
//            {
//                throw new ArgumentException("key already present");
//            }

//            int targetIndex = ~index;
//            if (count < 8)
//            {
//                BPlusTreeLeafNode<TKey, TValue> copy = new(this, countDelta: 1);
//                Span<Entry> copyStorageSpan = Storage.CreateSpan(ref copy.Entries);
//                if (targetIndex < 7)
//                {
//                    storageSpan.Slice(targetIndex).CopyTo(copyStorageSpan.Slice(targetIndex + 1));
//                }
//                copyStorageSpan[targetIndex] = new() { Key = key, Value = value };
//                split = null;
//                splitKey = default;
//                return copy;
//            }

//            BPlusTreeLeafNode<TKey, TValue> leftCopy = new(4, isFrozen: true),
//                rightCopy = new(4, isFrozen: true);
//            Span<Entry> leftCopyStorageSpan = Storage.CreateSpan(ref leftCopy.Entries),
//                rightCopyStorageSpan = Storage.CreateSpan(ref rightCopy.Entries);
//            var targetIndexOffset = 0;
//            for (var i = 0; i < 8; ++i)
//            {
//                if (i == targetIndex)
//                {
//                    targetIndexOffset = 1;
//                    if (i < 4)
//                    {
//                        leftCopyStorageSpan[i] = new() { Key = key, Value = value };
//                    }
//                    else
//                    {
//                        rightCopyStorageSpan[i - 4] = new() { Key = key, Value = value };
//                    }
//                }
//                else
//                {
//                    if (i < 4)
//                    {
//                        leftCopyStorageSpan[i] = storageSpan[i - targetIndexOffset];
//                    }
//                    else
//                    {
//                        rightCopyStorageSpan[i - 4] = storageSpan[i - targetIndexOffset];
//                    }
//                }
//            }

//            split = rightCopy;
//            splitKey = rightCopyStorageSpan[0].Key;
//            return leftCopy;
//        }

//        internal override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
//        {
//            Span<Entry> storageSpan = Storage.CreateSpan(ref this.Entries);
//            int index = storageSpan.Slice(0, this.Count).BinarySearch(new EntryComparable(key));
//            if (index >= 0)
//            {
//                value = storageSpan[index].Value;
//                return true;
//            }

//            value = default;
//            return false;
//        }

//        public struct Entry
//        {
//            internal TKey Key;
//            internal TValue Value;
//        }

//        private readonly struct EntryComparable : IComparable<Entry>
//        {
//            private readonly TKey _key;

//            public EntryComparable(TKey key)
//            {
//                this._key = key;
//            }
            
//            public int CompareTo(Entry other)
//            {
//                return Comparer<TKey>.Default.Compare(this._key, other.Key);
//            }
//        }
//    }

//    internal abstract class BPlusTreeInternalNode<TKey, TValue> : BPlusTreeNode<TKey, TValue>
//    {
//        internal Storage8<BPlusTreeNode<TKey, TValue>?> Children;

//        protected BPlusTreeInternalNode(BPlusTreeInternalNode<TKey, TValue> node, int countDelta) : base(node, countDelta)
//        {
//            this.Children = node.Children;
//        }

//        internal override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
//        {
//            BPlusTreeInternalNode<TKey, TValue> current = this;
//            while (true)
//            {
//                int index = current.IndexOf(key);
//                if (index < 0)
//                {
//                    value = default;
//                    return false;
//                }

//                Debug.Assert(index < this.Count);
//                BPlusTreeNode<TKey, TValue> child = Storage.CreateSpan(ref this.Children)[index]!;
//                Debug.Assert(child != null);
//                if (child is BPlusTreeInternalNode<TKey, TValue> internalChild)
//                {
//                    current = internalChild;
//                    // todo loop through duplicates here
//                }
//                else // leaf node
//                {
//                    return child.TryGetValue(key, out value);
//                }
//            }
//        }

//        internal override BPlusTreeNode<TKey, TValue> Add(TKey key, TValue value, out BPlusTreeNode<TKey, TValue>? split, out TKey? splitKey)
//        {
//            int index = this.IndexOf(key);

//            Span<BPlusTreeNode<TKey, TValue>?> childrenSpan = Storage.CreateSpan(ref this.Children);
//            BPlusTreeNode<TKey, TValue> child = childrenSpan[index]!;
//            Debug.Assert(child != null);

//            BPlusTreeNode<TKey, TValue> newChild = child.Add(key, value, out BPlusTreeNode<TKey, TValue>? childSplit, out TKey? childSplitKey);
//            if (newChild == child)
//            {
//                split = null;
//                splitKey = default;
//                return this;
//            }

//            return this.CopyWithChild(index, newChild, childSplit, childSplitKey, out split, out splitKey);
//        }

//        protected abstract int IndexOf(TKey key);

//        protected abstract BPlusTreeInternalNode<TKey, TValue> CopyWithChild(
//            int childIndex,
//            BPlusTreeNode<TKey, TValue> newChild,
//            BPlusTreeNode<TKey, TValue>? childSplit,
//            TKey? childSplitKey,
//            out BPlusTreeNode<TKey, TValue>? split,
//            out TKey? splitKey);
//    }

//    internal sealed class BPlusTreeInternalKeyNode<TKey, TValue> : BPlusTreeInternalNode<TKey, TValue>
//    {
//        internal Storage7<TKey> Keys;

//        private BPlusTreeInternalKeyNode(BPlusTreeInternalKeyNode<TKey, TValue> node, int countDelta)
//            : base(node, countDelta)
//        {
//            this.Keys = node.Keys;
//        }

//        protected override BPlusTreeInternalNode<TKey, TValue> CopyWithChild(
//            int childIndex, 
//            BPlusTreeNode<TKey, TValue> newChild, 
//            BPlusTreeNode<TKey, TValue>? childSplit, 
//            TKey? childSplitKey, 
//            out BPlusTreeNode<TKey, TValue>? split, 
//            out TKey? splitKey)
//        {
//            if (childSplit is null)
//            {
//                BPlusTreeInternalKeyNode<TKey, TValue> copy = new(this, countDelta: 0);
//                Storage.CreateSpan(ref copy.Children)[childIndex] = newChild;
//                split = null;
//                splitKey = default;
//                return copy;
//            }

//            int count = this.Count;
//            if (count < 8)
//            {
//                BPlusTreeInternalKeyNode<TKey, TValue> copy = new(this, countDelta: 1);
//                Span<BPlusTreeNode<TKey, TValue>?> copyChildrenSpan = Storage.CreateSpan(ref copy.Children);
//                for (int i = count; i > childIndex; --i)
//                {
//                    copyChildrenSpan[i] = copyChildrenSpan[i - 1];
//                }
//                copyChildrenSpan[childIndex + 1] = childSplit;

//                Span<TKey> copyKeysSpan = Storage.CreateSpan(ref copy.Keys);
//            }
//        }

//        protected override int IndexOf(TKey key)
//        {
//            int index = Storage.CreateSpan(ref this.Keys).Slice(0, this.Count - 1).BinarySearch(key, Comparer<TKey>.Default);
//            if (index >= 0)
//            {
//                return index + 1;
//            }

//            return ~index;
//        }
//    }

//    //internal sealed class BPlusTreeInternalHashNode<TKey, TValue> : BPlusTreeInternalNode<HashBucket<TKey>, TValue>
//    //{
//    //    internal Storage7<int> Hashes;

//    //    protected override int IndexOf(HashBucket<TKey> key)
//    //    {
//    //        int index = Storage.CreateSpan(ref this.Hashes).Slice(0, this.Count - 1).BinarySearch(key.Hash);
//    //        if (index >= 0)
//    //        {
//    //            return index + 1;
//    //        }

//    //        return ~index;
//    //    }
//    //}

//    internal struct HashBucket<TKey>
//    {
//        internal int Hash;
//        internal TKey Key;
//    }
//}
