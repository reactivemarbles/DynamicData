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
/// Represents the EnumerableIList value.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="list">The list value.</param>
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, different generics.")]
internal readonly struct EnumerableIList<T>(IList<T> list) : IEnumerableIList<T>, IList<T>
{
    /// <summary>
    /// Gets the Empty value.
    /// </summary>
    public static EnumerableIList<T> Empty { get; }

    /// <inheritdoc />
    public int Count => list.Count;

    /// <inheritdoc />
    public bool IsReadOnly => list.IsReadOnly;

    /// <inheritdoc />
    /// <param name="index">The index value.</param>
    public T this[int index]
    {
        get => list[index];
        set => list[index] = value;
    }

    /// <summary>
    /// Executes the conversion operator operation.
    /// </summary>
    /// <param name="list">The list value.</param>
    /// <returns>The result of the operation.</returns>
    public static implicit operator EnumerableIList<T>(List<T> list) => new(list);

    /// <summary>
    /// Executes the conversion operator operation.
    /// </summary>
    /// <param name="array">The array value.</param>
    /// <returns>The result of the operation.</returns>
    public static implicit operator EnumerableIList<T>(T[] array) => new(array);

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public EnumeratorIList<T> GetEnumerator() => new(list);

    /// <inheritdoc />
    /// <param name="item">The item value.</param>
    public void Add(T item) => list.Add(item);

    /// <inheritdoc />
    public void Clear() => list.Clear();

    /// <inheritdoc />
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Contains(T item) => list.Contains(item);

    /// <inheritdoc />
    /// <param name="array">The array value.</param>
    /// <param name="arrayIndex">The arrayIndex value.</param>
    public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public int IndexOf(T item) => list.IndexOf(item);

    /// <inheritdoc />
    /// <param name="index">The index value.</param>
    /// <param name="item">The item value.</param>
    public void Insert(int index, T item) => list.Insert(index, item);

    /// <inheritdoc />
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Remove(T item) => list.Remove(item);

    /// <inheritdoc />
    /// <param name="index">The index value.</param>
    public void RemoveAt(int index) => list.RemoveAt(index);

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Provides members for the EnumerableIList class.
/// </summary>
internal static class EnumerableIList
{
    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="list">The list value.</param>
    /// <returns>The result of the operation.</returns>
    public static EnumerableIList<T> Create<T>(IList<T> list) => new(list);

    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="changeSet">The changeSet value.</param>
    /// <returns>The result of the operation.</returns>
    public static EnumerableIList<Change<TObject, TKey>> Create<TObject, TKey>(IChangeSet<TObject, TKey> changeSet)
        where TObject : notnull
        where TKey : notnull =>
        Create((IList<Change<TObject, TKey>>)changeSet);
}
