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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Skips the initial changeset (the snapshot emitted on subscription) and forwards all subsequent changesets.
    /// Internally defers until loaded, then skips the first emission.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to skip the initial changeset.</param>
    /// <returns>A list changeset stream that omits the initial snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Warning:</b> This operator assumes the initial changeset is empty. If the source emits a non-empty
    /// initial snapshot, those items are silently dropped while downstream consumers remain unaware of them.
    /// Any later <b>Refresh</b>, <b>Replace</b>, <b>Remove</b>, or <b>Moved</b> change targeting one of those
    /// dropped items will throw because the downstream collection has no record of them. Only use this against
    /// a source you know starts empty (for example, a <c>ISourceList&lt;T&gt;</c> that has not yet been populated).
    /// </para>
    /// </remarks>
    /// <seealso><c>DeferUntilLoaded&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>StartWithEmpty&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> SkipInitial<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.DeferUntilLoaded().Skip(1);
    }
}
