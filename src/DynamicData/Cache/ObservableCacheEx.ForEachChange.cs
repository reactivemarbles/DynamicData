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
    /// Invokes <paramref name="action"/> for every individual <c>Change&lt;TObject,TKey&gt;</c> in each changeset,
    /// regardless of change reason. The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe each individual change in.</param>
    /// <param name="action">The action to invoke for each change. Receives the full <c>Change&lt;TObject,TKey&gt;</c> struct, including <c>Change&lt;TObject,TKey&gt;.Reason</c>, <c>Change&lt;TObject,TKey&gt;.Key</c>, <c>Change&lt;TObject,TKey&gt;.Current</c>, and <c>Change&lt;TObject,TKey&gt;.Previous</c>.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// All change reasons (Add, Update, Remove, Refresh) trigger the callback.
    /// Use <c>OnItemAdded&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TKey&gt;)</c>,
    /// <c>OnItemUpdated&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TObject,TKey&gt;)</c>,
    /// <c>OnItemRemoved&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TKey&gt;, bool)</c>, or
    /// <c>OnItemRefreshed&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TKey&gt;)</c>
    /// to target a specific reason.
    /// </para>
    /// <para>
    /// Implemented via Rx's <c>Do</c> operator on the changeset stream.
    /// Exceptions thrown in <paramref name="action"/> propagate as <c>OnError</c> to the subscriber. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <seealso><c>ObservableListEx.ForEachChange</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> ForEachChange<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<Change<TObject, TKey>> action)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(action);

        return source.Do(changes => changes.ForEach(action));
    }
}
