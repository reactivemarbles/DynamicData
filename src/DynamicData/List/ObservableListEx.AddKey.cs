// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Linq;
#else

using DynamicData.List.Linq;
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
    /// Adds a key to each item in a list changeset, converting it to a cache changeset that supports all keyed DynamicData operators.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to add keys to, converting to a cache changeset.</param>
    /// <param name="keySelector">A <c>Func&lt;T, TResult&gt;</c> function to extract a unique key from each item.</param>
    /// <returns>A cache <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> changeset stream with keyed items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All index information is dropped during conversion because cache changesets are unordered by default.
    /// Use this when you need to transition from list-based pipelines to cache-based operators (Filter by key, Join, Group, etc.).
    /// </para>
    /// </remarks>
    /// <seealso><c>ObservableCacheEx.RemoveKey&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);

        return source.Select(changes => new ChangeSet<TObject, TKey>(new AddKeyEnumerator<TObject, TKey>(changes, keySelector)));
    }
}
