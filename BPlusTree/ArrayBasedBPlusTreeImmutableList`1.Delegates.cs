using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    public partial class ArrayBasedBPlusTreeImmutableList<T>
    {
        private static class IndexOfDelegate
        {
            public static readonly Scanner<(int Remaining, T Item, IEqualityComparer<T> Comparer)> Instance = IndexOfHelper;

            private static bool IndexOfHelper(ReadOnlySpan<T> items, ref (int Remaining, T Item, IEqualityComparer<T> Comparer) state)
            {
                for (var i = 0; i < items.Length; i++)
                {
                    if (i >= state.Remaining)
                    {
                        break;
                    }
                    if (state.Comparer.Equals(items[i], state.Item))
                    {
                        state.Remaining -= i;
                        return true; // break
                    }
                }

                if ((state.Remaining -= items.Length) <= 0)
                {
                    state.Remaining = -1;
                    return true; // break
                }
                return false; // continue
            }
        }

        private static class CopyToDelegte
        {
            public static readonly Scanner<(T[] Array, int Index)> Instance = CopyToHelper;
            private static bool CopyToHelper(ReadOnlySpan<T> items, ref (T[] Array, int Index) state)
            {
                // todo can't handle covariant T[]; need special case for this
                items.CopyTo(state.Array.AsSpan(state.Index));
                state.Index += items.Length;
                return false;
            }
        }
    }
}
