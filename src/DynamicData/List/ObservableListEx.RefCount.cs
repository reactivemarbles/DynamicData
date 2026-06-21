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
    /// Reference-counted materialization of the source changeset stream into an <c>IObservableList&lt;T&gt;</c>.
    /// The shared list is created on the first subscriber and disposed when the last subscriber unsubscribes.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to share via reference counting.</param>
    /// <returns>A list changeset stream backed by a shared, reference-counted <c>IObservableList&lt;T&gt;</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Equivalent to <c>Publish().RefCount()</c> for changeset streams. The underlying list is created lazily on first subscription.</para>
    /// </remarks>
    /// <seealso><c>AsObservableList&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> RefCount<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new RefCount<T>(source).Run();
    }
}
