// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Collections.Immutable
{
    /// <summary>
    /// An interface that must be implemented by collections that want to avoid
    /// boxing their own enumerators when using the
    /// <see cref="ImmutableExtensions.GetEnumerableDisposable{T, TEnumerator}(IEnumerable{T})"/>
    /// method.
    /// </summary>
    /// <typeparam name="T">The type of value to be enumerated.</typeparam>
    /// <typeparam name="TEnumerator">The type of the enumerator struct.</typeparam>
    internal interface IStrongEnumerable<out T, TEnumerator>
        where TEnumerator : struct, IStrongEnumerator<T>
    {
        /// <summary>
        /// Gets the strongly-typed enumerator.
        /// </summary>
        /// <returns></returns>
        TEnumerator GetEnumerator();
    }

    /// <summary>
    /// An <see cref="IEnumerator{T}"/>-like interface that does not derive from <see cref="IDisposable"/>.
    /// </summary>
    /// <typeparam name="T">The type of value to be enumerated.</typeparam>
    /// <remarks>
    /// This interface is useful because some enumerator struct types do not want to implement
    /// <see cref="IDisposable"/> since it increases the size of the generated code in foreach.
    /// </remarks>
    internal interface IStrongEnumerator<T>
    {
        /// <summary>
        /// Returns the current element.
        /// </summary>
        T Current { get; }

        /// <summary>
        /// Advances to the next element.
        /// </summary>
        bool MoveNext();
    }
}