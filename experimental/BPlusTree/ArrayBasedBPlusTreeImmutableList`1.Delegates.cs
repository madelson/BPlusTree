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
            public static readonly Scanner<(int Index, T Item, IEqualityComparer<T> Comparer)> Instance = IndexOfHelper;

            private static bool IndexOfHelper(ReadOnlySpan<T> items, ref (int Index, T Item, IEqualityComparer<T> Comparer) state)
            {
                for (var i = 0; i < items.Length; i++)
                {
                    if (state.Comparer.Equals(items[i], state.Item))
                    {
                        state.Index += i;
                        return true; // break
                    }
                }

                state.Index += items.Length;
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

        private static class FindDelegate
        {
            public static readonly Scanner<(Predicate<T> Predicate, int FoundIndex, T? FoundItem)> Instance = FindHelper;

            private static bool FindHelper(ReadOnlySpan<T> items, ref (Predicate<T> Predicate, int FoundIndex, T? FoundItem) state)
            {
                for (var i = 0; i < items.Length; ++i)
                {
                    if (state.Predicate(items[i]))
                    {
                        state.FoundIndex += i;
                        state.FoundItem = items[i];
                        return true;
                    }
                }

                state.FoundIndex += items.Length;
                return false;
            }
        }

        private static class ForEachDelegate
        {
            public static readonly Scanner<Action<T>> Instance = ForEachHelper;

            private static bool ForEachHelper(ReadOnlySpan<T> items, ref Action<T> state)
            {
                Action<T> action = state;
                for (var i = 0; i < items.Length; ++i)
                {
                    action(items[i]);
                }
                return false;
            }
        }

        private static class TrueForAllDelegate
        {
            public static readonly Scanner<Predicate<T>> Instance = TrueForAllHelper;

            private static bool TrueForAllHelper(ReadOnlySpan<T> items, ref Predicate<T> state)
            {
                Predicate<T> match = state;
                for (var i = 0; i < items.Length; ++i)
                {
                    if (!match(items[i])) 
                    { 
                        return true; 
                    }
                }
                return false;
            }
        }
    }
}
