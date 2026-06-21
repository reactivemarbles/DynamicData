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
    /// Prepends an empty changeset to the source stream. Useful for initializing downstream consumers that expect an initial emission.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>A list changeset stream that begins with an empty changeset.</returns>
    /// <seealso><c>DeferUntilLoaded&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>SkipInitial&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.StartWithEmpty&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> StartWithEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(ChangeSet<T>.Empty);
    }
}
