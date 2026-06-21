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
    /// Re-keys each item in the changeset by applying <paramref name="keySelector"/> to the current item.
    /// The original change reason is preserved; only the key is remapped.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TSourceKey}}"/> to re-key.</param>
    /// <param name="keySelector">The <see cref="Func{TObject, TDestinationKey}"/> that computes the destination key from the item, e.g. <c>(item) =&gt; item.NewId</c>.</param>
    /// <returns>An observable changeset with items re-keyed using <paramref name="keySelector"/>.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description><paramref name="keySelector"/> is called on the item. An <b>Add</b> is emitted with the destination key.</description></item>
    /// <item><term>Update</term><description><paramref name="keySelector"/> is called on the current item. An <b>Update</b> is emitted with the destination key. If the key selector produces a different destination key for the updated value than it did for the original value, downstream consumers will see an <b>Update</b> for a key that may not match the original <b>Add</b>.</description></item>
    /// <item><term>Remove</term><description><paramref name="keySelector"/> is called on the item. A <b>Remove</b> is emitted with the destination key.</description></item>
    /// <item><term>Refresh</term><description><paramref name="keySelector"/> is called on the item. A <b>Refresh</b> is emitted with the destination key.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/>
    public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TObject, TDestinationKey> keySelector)
        where TObject : notnull
        where TSourceKey : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);

        return source.Select(
            updates =>
            {
                var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Current), u.Current, u.Previous));
                return new ChangeSet<TObject, TDestinationKey>(changed);
            });
    }

    /// <inheritdoc cref="ChangeKey{TObject, TSourceKey, TDestinationKey}(IObservable{IChangeSet{TObject, TSourceKey}}, Func{TObject, TDestinationKey})"/>
    /// <remarks>
    /// This overload also provides the source key to <paramref name="keySelector"/>,
    /// allowing the destination key to be derived from both the item and its original key.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TSourceKey, TObject, TDestinationKey> keySelector)
        where TObject : notnull
        where TSourceKey : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);

        return source.Select(
            updates =>
            {
                var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Key, u.Current), u.Current, u.Previous));
                return new ChangeSet<TObject, TDestinationKey>(changed);
            });
    }
}
