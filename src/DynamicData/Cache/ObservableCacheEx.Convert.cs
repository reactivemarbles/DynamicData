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
    /// Obsolete: use <see cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/> instead.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to convert.</param>
    /// <param name="conversionFactory">The <see cref="Func{TObject, TDestination}"/> conversion factory.</param>
    /// <returns>An observable which emits change sets.</returns>
    [Obsolete("This was an experiment that did not work. Use Transform instead")]
    public static IObservable<IChangeSet<TDestination, TKey>> Convert<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TDestination> conversionFactory)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(
            changes =>
            {
                var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason, change.Key, conversionFactory(change.Current), change.Previous.Convert(conversionFactory), change.CurrentIndex, change.PreviousIndex));
                return new ChangeSet<TDestination, TKey>(transformed);
            });
    }
}
