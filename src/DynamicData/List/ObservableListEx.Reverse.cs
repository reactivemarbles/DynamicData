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
    /// Reverses the order of items in the changeset stream by transforming all indices: <c>new_index = length - old_index - 1</c>.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to reverse.</param>
    /// <returns>A list changeset stream with all index positions reversed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a pure index transformation. The items themselves are unchanged; only their positional indices are inverted.</para>
    /// </remarks>
    /// <seealso cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    public static IObservable<IChangeSet<T>> Reverse<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        var reverser = new Reverser<T>();
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Select(changes => new ChangeSet<T>(reverser.Reverse(changes)));
    }
}
