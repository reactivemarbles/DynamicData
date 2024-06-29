// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;

namespace DynamicData.List.Internal;

[DebuggerDisplay("AnonymousObservableList<{typeof(T).Name}> ({Count} Items)")]
internal sealed class AnonymousObservableList<T> : IObservableList<T>
    where T : notnull
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed through _cleanUp")]
    private readonly ISourceList<T> _sourceList;
    private readonly IDisposable _cleanUp;

    public AnonymousObservableList(IObservable<IChangeSet<T>> source)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        _sourceList = new SourceList<T>(source);
        _cleanUp = _sourceList;
    }

    public AnonymousObservableList(ISourceList<T> sourceList)
    {
        _sourceList = sourceList ?? throw new ArgumentNullException(nameof(sourceList));
        _cleanUp = Disposable.Empty;
    }

    public int Count => _sourceList.Count;

    public IObservable<int> CountChanged => _sourceList.CountChanged;

    public IReadOnlyList<T> Items => _sourceList.Items;

    public IObservable<IChangeSet<T>> Connect(Func<T, bool>? predicate = null) => _sourceList.Connect(predicate);

    public IObservable<IChangeSet<T>> Preview(Func<T, bool>? predicate = null) => _sourceList.Preview(predicate);

    public void Dispose() => _cleanUp.Dispose();
}
