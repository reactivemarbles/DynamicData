// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the AnonymousObservableList class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
[DebuggerDisplay("AnonymousObservableList<{typeof(T).Name}> ({Count} Items)")]
internal sealed class AnonymousObservableList<T> : IObservableList<T>
    where T : notnull
{
    /// <summary>
    /// The _sourceList field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed through _cleanUp")]
    private readonly ISourceList<T> _sourceList;

    /// <summary>
    /// The _cleanUp field.
    /// </summary>
    private readonly IDisposable _cleanUp;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousObservableList{T}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    public AnonymousObservableList(IObservable<IChangeSet<T>> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        _sourceList = new SourceList<T>(source);
        _cleanUp = _sourceList;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousObservableList{T}"/> class.
    /// </summary>
    /// <param name="sourceList">The sourceList value.</param>
    public AnonymousObservableList(ISourceList<T> sourceList)
    {
        ArgumentExceptionHelper.ThrowIfNull(sourceList);
        _sourceList = sourceList;
        _cleanUp = Disposable.Empty;
    }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _sourceList.Count;

    /// <summary>
    /// Gets the CountChanged value.
    /// </summary>
    public IObservable<int> CountChanged => _sourceList.CountChanged;

    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IReadOnlyList<T> Items => _sourceList.Items;

    /// <summary>
    /// Executes the Connect operation.
    /// </summary>
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Connect(Func<T, bool>? predicate = null) => _sourceList.Connect(predicate);

    /// <summary>
    /// Executes the Preview operation.
    /// </summary>
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Preview(Func<T, bool>? predicate = null) => _sourceList.Preview(predicate);

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose() => _cleanUp.Dispose();
}
