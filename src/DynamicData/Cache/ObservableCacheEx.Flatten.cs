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
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Unwraps each <c>IChangeSet&lt;TObject, TKey&gt;</c> into individual <c>Change&lt;TObject, TKey&gt;</c>
    /// values via <c>Observable.SelectMany&lt;TSource, TResult&gt;(IObservable&lt;TSource&gt;, Func&lt;TSource, IEnumerable&lt;TResult&gt;&gt;)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to flatten into individual changes.</param>
    /// <returns>An observable of individual <c>Change&lt;TObject, TKey&gt;</c> values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>ForEachChange&lt;TObject, TKey&gt;</c></seealso>
    public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.SelectMany(changes => changes);
    }
}
