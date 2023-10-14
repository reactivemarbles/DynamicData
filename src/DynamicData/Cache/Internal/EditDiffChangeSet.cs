﻿// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class EditDiffChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IEnumerable<TObject>> _source;

    private readonly IEqualityComparer<TObject> _equalityComparer;

    private readonly Func<TObject, TKey> _keySelector;

    public EditDiffChangeSet(IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run() =>
        ObservableChangeSet.Create(
            cache => _source.Subscribe(items => cache.EditDiff(items, _equalityComparer), () => cache.Dispose()),
            _keySelector);
}
