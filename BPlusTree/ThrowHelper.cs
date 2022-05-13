using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    internal static class ThrowHelper
    {
        [DoesNotReturn]
        public static void ThrowArgumentOutOfRange(string paramName = "index") =>
            throw new ArgumentOutOfRangeException(paramName, paramName + " Must be non-negative and less than the size of the collection.");

        [DoesNotReturn]
        public static void ThrowArgumentNull(string parameterName) =>
            throw new ArgumentNullException(parameterName);

        [DoesNotReturn]
        public static void ThrowVersionChanged() =>
            throw new InvalidOperationException("Collection was modified during enumeration");

        [DoesNotReturn]
        public static void ThrowObjectDisposed<T>(T value) =>
            throw new ObjectDisposedException((value?.GetType() ?? typeof(T)).ToString());

        [DoesNotReturn]
        public static void ThrowCannotFindOldValue() =>
            throw new ArgumentException("Cannot find the old value", "oldValue");
    }
}
