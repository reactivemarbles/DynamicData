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
    /// Invokes <paramref name="action"/> for every individual <see cref="Change{TObject,TKey}"/> in each changeset,
    /// regardless of change reason. The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe each individual change in.</param>
    /// <param name="action">The action to invoke for each change. Receives the full <see cref="Change{TObject,TKey}"/> struct, including <see cref="Change{TObject,TKey}.Reason"/>, <see cref="Change{TObject,TKey}.Key"/>, <see cref="Change{TObject,TKey}.Current"/>, and <see cref="Change{TObject,TKey}.Previous"/>.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// All change reasons (Add, Update, Remove, Refresh) trigger the callback.
    /// Use <see cref="OnItemAdded{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey})"/>,
    /// <see cref="OnItemUpdated{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TObject,TKey})"/>,
    /// <see cref="OnItemRemoved{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey}, bool)"/>, or
    /// <see cref="OnItemRefreshed{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey})"/>
    /// to target a specific reason.
    /// </para>
    /// <para>
    /// Implemented via Rx's <c>Do</c> operator on the changeset stream.
    /// Exceptions thrown in <paramref name="action"/> propagate as <c>OnError</c> to the subscriber. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ObservableListEx.ForEachChange"/>
    public static IObservable<IChangeSet<TObject, TKey>> ForEachChange<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<Change<TObject, TKey>> action)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(action);

        return source.Do(changes => changes.ForEach(action));
    }
}
