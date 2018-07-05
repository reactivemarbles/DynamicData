// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root (on link below) for license information.

//Lifted from here https://github.com/benaadams/Ben.Enumerable. Many thanks to the genius of the man.

using System.Collections;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
 
    internal static class EnumerableIList
    {
        public static EnumerableIList<Change<TObject, TKey>> ToEnumerableChangeSet<TObject, TKey>(this IChangeSet<TObject, TKey> changeset) => Create(changeset);

        public static EnumerableIList<T> Create<T>(IList<T> list) => new EnumerableIList<T>(list);

        public static EnumerableIList<Change<TObject, TKey>> Create<TObject, TKey>(IChangeSet<TObject, TKey> changeset) => Create((IList<Change<TObject, TKey>>)changeset);
    }


    internal struct EnumeratorIList<T> : IEnumerator<T>
    {
        private readonly IList<T> _list;
        private int _index;

        public EnumeratorIList(IList<T> list)
        {
            _index = -1;
            _list = list;
        }

        public T Current => _list[_index];

        public bool MoveNext()
        {
            _index++;

            return _index < (_list?.Count ?? 0);
        }

        public void Dispose() { }
        object IEnumerator.Current => Current;
        public void Reset() => _index = -1;
    }

    internal interface IEnumerableIList<T> : IEnumerable<T>
    {
        new EnumeratorIList<T> GetEnumerator();
    }

    internal readonly struct EnumerableIList<T> : IEnumerableIList<T>, IList<T>
    {
        private readonly IList<T> _list;

        public EnumerableIList(IList<T> list)
        {
            _list = list;
        }

        public EnumeratorIList<T> GetEnumerator() => new EnumeratorIList<T>(_list);

        public static implicit operator EnumerableIList<T>(List<T> list) => new EnumerableIList<T>(list);

        public static implicit operator EnumerableIList<T>(T[] array) => new EnumerableIList<T>(array);


        public static EnumerableIList<T> Empty = default;


        // IList pass through

        /// <inheritdoc />
        public T this[int index] { get => _list[index]; set => _list[index] = value; }

        /// <inheritdoc />
        public int Count => _list.Count;

        /// <inheritdoc />
        public bool IsReadOnly => _list.IsReadOnly;

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
}