// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Source cache convenience extensions.
/// </summary>
public static class SourceCacheEx
{
    /// <summary>
    /// Connects to the cache, and casts the object to the specified type
    /// Alas, I had to add the converter due to type inference issues.
    /// </summary>
    /// <typeparam name="TSource">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The conversion factory.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(this IObservableCache<TSource, TKey> source, Func<TSource, TDestination> converter)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull => source is null ? throw new ArgumentNullException(nameof(source)) : source.Connect().Cast(converter);
}
