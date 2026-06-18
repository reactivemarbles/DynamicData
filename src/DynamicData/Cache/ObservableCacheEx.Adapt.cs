// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Binding;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Injects a side effect into the changeset stream by calling <paramref name="adaptor"/>.<see cref="IChangeSetAdaptor{TObject, TKey}.Adapt(IChangeSet{TObject, TKey})"/>
    /// for every changeset, then forwarding it downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe and adapt.</param>
    /// <param name="adaptor">The <see cref="IChangeSetAdaptor{TObject, TKey}"/> whose Adapt method is called for each changeset.</param>
    /// <returns>An observable that emits the same changesets as <paramref name="source"/>, after the adaptor has processed each one.</returns>
    /// <remarks>
    /// <para>
    /// This is a thin wrapper around Rx's <c>Do</c> operator. The adaptor receives each changeset
    /// as a side effect; the changeset itself is forwarded downstream unmodified.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="adaptor"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Adapt{TObject, TKey}(IObservable{ISortedChangeSet{TObject, TKey}}, ISortedChangeSetAdaptor{TObject, TKey})"/>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservableCollection{TObject}, IObservableCollectionAdaptor{TObject, TKey})"/>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return source.Do(adaptor.Adapt);
    }

    /// <inheritdoc cref="Adapt{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IChangeSetAdaptor{TObject, TKey})"/>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to observe and adapt.</param>
    /// <param name="adaptor">The <see cref="ISortedChangeSetAdaptor{TObject, TKey}"/> whose Adapt method is called for each changeset.</param>
    /// <remarks>This overload operates on <see cref="ISortedChangeSet{TObject, TKey}"/>. Delegates to Rx's <c>Do</c> operator.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, ISortedChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return source.Do(adaptor.Adapt);
    }
}
