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
    /// <summary>
    /// Skips the initial snapshot changeset that <c>Connect()</c> typically emits, then forwards all subsequent changesets.
    /// Internally uses <c>DeferUntilLoaded().Skip(1)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to skip the initial changeset.</param>
    /// <returns>An observable that skips the first changeset and forwards all others.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DeferUntilLoaded{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    /// <seealso cref="StartWithEmpty{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> SkipInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.DeferUntilLoaded().Skip(1);
    }
}
