// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Lifted from here https://github.com/benaadams/Ben.Enumerable. Many thanks to the genius of the man.
#if REACTIVE_SHIM
namespace DynamicData.Reactive.Kernel;
#else
namespace DynamicData.Kernel;
#endif

/// <summary>
/// Represents the EnumeratorIList value.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="list">The list value.</param>
internal struct EnumeratorIList<T>(IList<T> list) : IEnumerator<T>
{
    /// <summary>
    /// The _index field.
    /// </summary>
    private int _index = -1;

    /// <summary>
    /// Gets the Current value.
    /// </summary>
    public readonly T Current => list[_index];

    /// <summary>
    /// Gets the Current value.
    /// </summary>
    readonly object? IEnumerator.Current => Current;

    /// <summary>
    /// Executes the MoveNext operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public bool MoveNext()
    {
        _index++;

        return _index < list.Count;
    }

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Executes the Reset operation.
    /// </summary>
    public void Reset() => _index = -1;
}
