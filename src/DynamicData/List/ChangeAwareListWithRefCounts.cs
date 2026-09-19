// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Provides members for the ChangeAwareListWithRefCounts class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class ChangeAwareListWithRefCounts<T> : ChangeAwareList<T>
    where T : notnull
{
    /// <summary>
    /// The _tracker field.
    /// </summary>
    private readonly ReferenceCountTracker<T> _tracker = new();

    /// <summary>
    /// Executes the Clear operation.
    /// </summary>
    public override void Clear()
    {
        _tracker.Clear();
        base.Clear();
    }

    /// <summary>
    /// Executes the Contains operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Contains(T item) => _tracker.Contains(item);

    /// <summary>
    /// Executes the InsertItem operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <param name="item">The item value.</param>
    protected override void InsertItem(int index, T item)
    {
        _tracker.Add(item);
        base.InsertItem(index, item);
    }

    /// <summary>
    /// Executes the OnInsertItems operation.
    /// </summary>
    /// <param name="startIndex">The startIndex value.</param>
    /// <param name="items">The items value.</param>
    protected override void OnInsertItems(int startIndex, IEnumerable<T> items) => items.ForEach(t => _tracker.Add(t));

    /// <summary>
    /// Executes the OnRemoveItems operation.
    /// </summary>
    /// <param name="startIndex">The startIndex value.</param>
    /// <param name="items">The items value.</param>
    protected override void OnRemoveItems(int startIndex, IEnumerable<T> items) => items.ForEach(t => _tracker.Remove(t));

    /// <summary>
    /// Executes the OnSetItem operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <param name="newItem">The newItem value.</param>
    /// <param name="oldItem">The oldItem value.</param>
    protected override void OnSetItem(int index, T newItem, T oldItem)
    {
        _tracker.Remove(oldItem);
        _tracker.Add(newItem);
        base.OnSetItem(index, newItem, oldItem);
    }

    /// <summary>
    /// Executes the RemoveItem operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <param name="item">The item value.</param>
    protected override void RemoveItem(int index, T item)
    {
        _tracker.Remove(item);
        base.RemoveItem(index, item);
    }
}
