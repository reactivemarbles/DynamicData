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
    /// Strips mutation indices, omits moves, and preserves the required source index of Refresh notifications.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to strip index information.</param>
    /// <returns>A list changeset stream with mutation indices removed and Refresh source indices retained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Refresh indices refer to the original source positions. Other mutation indices are removed, and moves are omitted because they require index positions.</para>
    /// </remarks>
    /// <seealso><c>ChangeSetEx.YieldWithoutIndex&lt;T&gt;(IEnumerable&lt;Change&lt;T&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> RemoveIndex<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Select(changes => new ChangeSet<T>(changes.YieldWithoutIndex()));
    }
}
