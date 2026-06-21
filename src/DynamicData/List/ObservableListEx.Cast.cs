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
    /// Casts each item in the changeset from <c>object</c> to <typeparamref name="TDestination"/> using a direct cast.
    /// </summary>
    /// <typeparam name="TDestination">The target type to cast to.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{object}}"/> of <c>object</c> items.</param>
    /// <returns>A list changeset stream of cast items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Cast{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination})"/>
    /// <seealso cref="CastToObject{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<TDestination>> Cast<TDestination>(this IObservable<IChangeSet<object>> source)
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Select(changes => changes.Transform(t => (TDestination)t));
    }

    /// <summary>
    /// Transforms each item in the changeset using a conversion function.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TDestination">The destination item type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> to cast.</param>
    /// <param name="conversionFactory">A <see cref="Func{T, TResult}"/> function to convert each item from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.</param>
    /// <returns>A list changeset stream of converted items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="conversionFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>Use this overload when type inference requires explicit specification of both source and destination types. Alternatively, call <see cref="CastToObject{T}"/> first, then the single-type-parameter <see cref="Cast{TDestination}"/> overload.</remarks>
    /// <seealso cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> conversionFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(conversionFactory);

        return source.Select(changes => changes.Transform(conversionFactory));
    }
}
