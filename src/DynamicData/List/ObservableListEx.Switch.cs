// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Subscribes to the latest inner <c>IObservableList&lt;T&gt;</c>, switching to each new source and clearing the result when switching.
    /// This is the changeset-aware equivalent of Rx's <c>Observable.Switch&lt;TSource&gt;(IObservable&lt;IObservable&lt;TSource&gt;&gt;)</c>, which cannot be applied directly to changeset streams.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An observable that emits <c>IObservableList&lt;T&gt;</c> instances. Each emission triggers a switch to the new list.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Convenience overload that calls <c>Connect()</c> on each inner list, then delegates to <c>Switch&lt;T&gt;(IObservable&lt;IObservable&lt;IChangeSet&lt;T&gt;&gt;&gt;)</c>.</para>
    /// </remarks>
    /// <seealso><c>Switch&lt;T&gt;(IObservable&lt;IObservable&lt;IChangeSet&lt;T&gt;&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservableList<T>> sources)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Subscribes to the latest inner changeset stream, switching to each new source and clearing the destination when switching.
    /// Previous subscriptions are disposed and the result set is emptied before subscribing to the new inner stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An <c>IObservable&lt;T&gt;</c> of <c>IObservable&lt;T&gt;</c> changeset streams. The operator subscribes to the latest inner stream.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// On each new inner stream, the operator clears the destination, disposes the previous subscription, and subscribes to the new stream.
    /// This is the changeset-aware equivalent of Rx's <c>Switch()</c>.
    /// </para>
    /// </remarks>
    /// <seealso><c>Switch&lt;T&gt;(IObservable&lt;IObservableList&lt;T&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservable<IChangeSet<T>>> sources)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return new Switch<T>(sources).Run();
    }
}
