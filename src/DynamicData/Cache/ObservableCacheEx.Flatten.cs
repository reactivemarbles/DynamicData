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
    /// Unwraps each <see cref="IChangeSet{TObject, TKey}"/> into individual <see cref="Change{TObject, TKey}"/>
    /// values via <see cref="Observable.SelectMany{TSource, TResult}(IObservable{TSource}, Func{TSource, IEnumerable{TResult}})"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to flatten into individual changes.</param>
    /// <returns>An observable of individual <see cref="Change{TObject, TKey}"/> values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ForEachChange{TObject, TKey}"/>
    public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.SelectMany(changes => changes);
    }
}
