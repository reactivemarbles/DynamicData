// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Predicate<Change<TObject, TKey>> predicate, Action<Change<TObject, TKey>> changeAction)
        where TObject : notnull
        where TKey : notnull
    {
        return source.Do(changes =>
        {
            foreach (var change in changes.ToConcreteType())
            {
                if (!predicate(change))
                {
                    continue;
                }

                changeAction(change);
            }
        });
    }

    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ChangeReason reason, Action<TObject, TKey> action)
        where TObject : notnull
        where TKey : notnull
        => source.OnChangeAction(change => change.Reason == reason, change => action(change.Current, change.Key));
}
