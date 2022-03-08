// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// Lifted from here https://github.com/benaadams/Ben.Enumerable. Many thanks to the genius of the man.
namespace DynamicData.Kernel;

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, different generics.")]
internal readonly struct EnumerableIList<T> : IEnumerableIList<T>, IList<T>
{
    private readonly IList<T> _list;

    public EnumerableIList(IList<T> list)
    {
        _list = list;
    }

    public static EnumerableIList<T> Empty { get; }

    /// <inheritdoc />
    public int Count => _list.Count;

    /// <inheritdoc />
    public bool IsReadOnly => _list.IsReadOnly;

    /// <inheritdoc />
    public T this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    public static implicit operator EnumerableIList<T>(List<T> list) => new(list);

    public static implicit operator EnumerableIList<T>(T[] array) => new(array);

    public EnumeratorIList<T> GetEnumerator() => new(_list);

    /// <inheritdoc />
    public void Add(T item) => _list.Add(item);

    /// <inheritdoc />
    public void Clear() => _list.Clear();

    /// <inheritdoc />
    public bool Contains(T item) => _list.Contains(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(T item) => _list.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, T item) => _list.Insert(index, item);

    /// <inheritdoc />
    public bool Remove(T item) => _list.Remove(item);

    /// <inheritdoc />
    public void RemoveAt(int index) => _list.RemoveAt(index);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
}

internal static class EnumerableIList
{
    public static EnumerableIList<T> Create<T>(IList<T> list) => new(list);

    public static EnumerableIList<Change<TObject, TKey>> Create<TObject, TKey>(IChangeSet<TObject, TKey> changeSet)
        where TKey : notnull =>
        Create((IList<Change<TObject, TKey>>)changeSet);
}
