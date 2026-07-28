// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Subscribes to the latest inner <see cref="IObservableList{T}"/>, switching to each new source and clearing the result when switching.
    /// This is the changeset-aware equivalent of Rx's <see cref="Observable.Switch{TSource}(IObservable{IObservable{TSource}})"/>, which cannot be applied directly to changeset streams.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An observable that emits <see cref="IObservableList{T}"/> instances. Each emission triggers a switch to the new list.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Convenience overload that calls <c>Connect()</c> on each inner list, then delegates to <see cref="Switch{T}(IObservable{IObservable{IChangeSet{T}}})"/>.</para>
    /// </remarks>
    /// <seealso cref="Switch{T}(IObservable{IObservable{IChangeSet{T}}})"/>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservableList<T>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Subscribes to the latest inner changeset stream, switching to each new source and clearing the destination when switching.
    /// Previous subscriptions are disposed and the result set is emptied before subscribing to the new inner stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An <see cref="IObservable{T}"/> of <see cref="IObservable{T}"/> changeset streams. The operator subscribes to the latest inner stream.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Each switch clears the entire downstream list before populating from the new source. Subscribers see a full remove-then-add reset on every switch.</para>
    /// <para><b>Also worth noting:</b>This operator intentionally shadows the native <see cref="Observable.Switch{TSource}(IObservable{IObservable{TSource}})"/> operator, as downstream listeners will generally become corrupt when the native operator is used. This is due to its lack of the automatic-clearing behavior mentioned above.</para>
    /// </remarks>
    /// <seealso cref="Switch{T}(IObservable{IObservableList{T}})"/>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservable<IChangeSet<T>>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Switch<T>(sources).Run();
    }
}
