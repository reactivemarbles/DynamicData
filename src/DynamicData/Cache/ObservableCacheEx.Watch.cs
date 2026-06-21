// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Filters the source changeset stream to a single key, emitting each <see cref="Change{TObject, TKey}"/> for that key.
    /// Changes for all other keys are ignored.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>An observable of <see cref="Change{TObject, TKey}"/> for the specified key only.</returns>
    /// <remarks>
    /// <para>
    /// Emits Add, Update, Remove, and Refresh changes as they occur for the target key.
    /// No initial emission occurs if the key is not yet present in the cache. This operator does not
    /// produce changesets; it produces individual change notifications. For Optional-based watching,
    /// use <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>
    public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.SelectMany(updates => updates).Where(update => update.Key.Equals(key));
    }
}
