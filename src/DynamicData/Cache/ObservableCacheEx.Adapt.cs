// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif

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
    /// Injects a side effect into the changeset stream by calling <paramref name="adaptor"/>.<c>IChangeSetAdaptor&lt;TObject, TKey&gt;.Adapt(IChangeSet&lt;TObject, TKey&gt;)</c>
    /// for every changeset, then forwarding it downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe and adapt.</param>
    /// <param name="adaptor">The <c>IChangeSetAdaptor&lt;TObject, TKey&gt;</c> whose Adapt method is called for each changeset.</param>
    /// <returns>An observable that emits the same changesets as <paramref name="source"/>, after the adaptor has processed each one.</returns>
    /// <remarks>
    /// <para>
    /// This is a thin wrapper around Rx's <c>Do</c> operator. The adaptor receives each changeset
    /// as a side effect; the changeset itself is forwarded downstream unmodified.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="adaptor"/> is <see langword="null"/>.</exception>
    /// <seealso><c>Adapt&lt;TObject, TKey&gt;(IObservable&lt;ISortedChangeSet&lt;TObject, TKey&gt;&gt;, ISortedChangeSetAdaptor&lt;TObject, TKey&gt;)</c></seealso>
    /// <seealso><c>Bind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservableCollection&lt;TObject&gt;, IObservableCollectionAdaptor&lt;TObject, TKey&gt;)</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(adaptor);

        return source.Do(adaptor.Adapt);
    }

    /// <summary>
    /// Provides an overload of <c>Adapt</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;ISortedChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe and adapt.</param>
    /// <param name="adaptor">The <c>ISortedChangeSetAdaptor&lt;TObject, TKey&gt;</c> whose Adapt method is called for each changeset.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload operates on <c>ISortedChangeSet&lt;TObject, TKey&gt;</c>. Delegates to Rx's <c>Do</c> operator.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, ISortedChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(adaptor);

        return source.Do(adaptor.Adapt);
    }
}
