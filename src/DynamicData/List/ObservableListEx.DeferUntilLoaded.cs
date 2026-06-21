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
    /// Defers downstream delivery until the source emits its first changeset, then forwards all subsequent changesets.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to defer until the first changeset arrives.</param>
    /// <returns>A list changeset stream that begins emitting only after the source has produced its first changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Subscribes to the source immediately but buffers internally until the first changeset arrives, at which point it emits
    /// the initial data and all subsequent changesets. This is useful when downstream consumers should not receive an empty initial state.
    /// </para>
    /// </remarks>
    /// <seealso><c>SkipInitial&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>StartWithEmpty&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new DeferUntilLoaded<T>(source).Run();
    }

    /// <summary>
    /// Provides an overload of <c>DeferUntilLoaded</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>Convenience overload that calls <c>source.Connect().DeferUntilLoaded()</c>.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservableList<T> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Connect().DeferUntilLoaded();
    }
}
