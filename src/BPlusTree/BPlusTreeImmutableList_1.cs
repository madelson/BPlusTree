using System.Collections;
using System.Diagnostics;

namespace Medallion.Collections;

public sealed partial class BPlusTreeImmutableList<T> : IEnumerable<T>
{
    public static readonly BPlusTreeImmutableList<T> Empty = new(Array.Empty<LeafEntry>(), count: 0);

    private readonly Array _root;
    private readonly int _count;

    private BPlusTreeImmutableList(Array root, int count)
    {
        AssertValid(root, count);
        Debug.Assert(root.Length > 0 || Empty is null, "Empty field must store the only empty instance");
        
        if (count < 0) { Helpers.ThrowCountOverflow(); }

        this._root = root;
        this._count = count;
    }

    public int Count => _count;
    public bool IsEmpty => _count == 0;

    public T this[int index] => ItemRef(index);

    public ref readonly T ItemRef(int index)
    {
        if ((uint)index >= (uint)_count) { Helpers.ThrowIndexOutOfRange(); }

        Array current = _root;
        while (true)
        {
            if (current.IsIndexNode())
            {
                IndexEntry[] currentIndex = current.ToIndex();
                int entryIndex = currentIndex.FindEntry(index);
                if (entryIndex != 0)
                {
                    index -= currentIndex[entryIndex - 1].Offset;
                }
                current = currentIndex[entryIndex].Child;
            }
            else
            {
                return ref ToLeaf(current)[index].Item;
            }
        }
    }

    public BPlusTreeImmutableList<T> AddRange(IEnumerable<T> items)
    {
        Helpers.ThrowIfNull(items);

        Array newRoot;
        int count;
        if (items is BPlusTreeImmutableList<T> immutableList)
        {
            if (IsEmpty) { return immutableList; }
            if (immutableList.IsEmpty) { return this; }

            using AppendOnlyBuilder builder = new();
            builder.AddNode(_root);
            builder.AddNode(immutableList._root);
            newRoot = builder.MoveToNode(out count, out _);
        }
        else
        {
            using var enumerator = items.GetEnumerator();
            if (!enumerator.MoveNext()) { return this; }

            using AppendOnlyBuilder builder = new();
            if (!IsEmpty) { builder.AddNode(_root); }

            do
            {
                builder.Add(enumerator.Current);
            }
            while (enumerator.MoveNext());

            newRoot = builder.MoveToNode(out count, out _);
        }

        return new(newRoot, count);
    }

    // todo replace
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _count; ++i) { yield return this[i]; }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
