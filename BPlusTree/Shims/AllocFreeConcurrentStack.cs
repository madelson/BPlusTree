using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
 
namespace System.Collections.Immutable
{
    internal static class AllocFreeConcurrentStack<T>
    {
        private const int MaxSize = 35;

        [ThreadStatic]
        private static Stack<RefAsValueType<T>>? _threadLocalStack;

        public static void TryAdd(T item)
        {
            // Just in case we're in a scenario where an object is continually requested on one thread
            // and returned on another, avoid unbounded growth of the stack.
            Stack<RefAsValueType<T>> localStack = ThreadLocalStack;
            if (localStack.Count < MaxSize)
            {
                localStack.Push(new RefAsValueType<T>(item));
            }
        }

        public static bool TryTake([MaybeNullWhen(false)] out T item)
        {
            Stack<RefAsValueType<T>> localStack = ThreadLocalStack;
            if (localStack != null && localStack.Count > 0)
            {
                item = localStack.Pop().Value;
                return true;
            }

            item = default;
            return false;
        }

        private static Stack<RefAsValueType<T>> ThreadLocalStack => _threadLocalStack ??= new();
    }

    [DebuggerDisplay("{Value,nq}")]
    internal struct RefAsValueType<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RefAsValueType{T}"/> struct.
        /// </summary>
        internal RefAsValueType(T value)
        {
            this.Value = value;
        }

        /// <summary>
        /// The value.
        /// </summary>
        internal T Value;
    }
}