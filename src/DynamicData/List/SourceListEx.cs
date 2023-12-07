// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Source list extensions.
/// </summary>
public static class SourceListEx
{
    /// <summary>
    /// <para>Connects to the list, and converts the changes to another form.</para>
    /// <para>Alas, I had to add the converter due to type inference issues.</para>
    /// </summary>
    /// <typeparam name="TSource">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="conversionFactory">The conversion factory.</param>
    /// <returns>An observable which emits that change set.</returns>
    public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>(this ISourceList<TSource> source, Func<TSource, TDestination> conversionFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Connect().Cast(conversionFactory);
    }
}
