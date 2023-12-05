// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

// Lifted from here https://github.com/benaadams/Ben.Enumerable. Many thanks to the genius of the man.
namespace DynamicData.Kernel;

internal struct EnumeratorIList<T>(IList<T> list) : IEnumerator<T>
{
    private int _index = -1;

    public readonly T Current => list[_index];

    readonly object? IEnumerator.Current => Current;

    public bool MoveNext()
    {
        _index++;

        return _index < list.Count;
    }

    public void Dispose()
    {
    }

    public void Reset() => _index = -1;
}
