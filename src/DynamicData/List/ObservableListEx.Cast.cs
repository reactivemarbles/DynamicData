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
    /// Casts each item in the changeset from <c>object</c> to <typeparamref name="TDestination"/> using a direct cast.
    /// </summary>
    /// <typeparam name="TDestination">The target type to cast to.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;object&gt;&gt;</c> of <c>object</c> items.</param>
    /// <returns>A list changeset stream of cast items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>Cast&lt;TSource, TDestination&gt;(IObservable&lt;IChangeSet&lt;TSource&gt;&gt;, Func&lt;TSource, TDestination&gt;)</c></seealso>
    /// <seealso><c>CastToObject&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource&gt;&gt;</c> to cast.</param>
    /// <param name="conversionFactory">A <c>Func&lt;T, TResult&gt;</c> function to convert each item from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.</param>
    /// <returns>A list changeset stream of converted items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="conversionFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>Use this overload when type inference requires explicit specification of both source and destination types. Alternatively, call <c>CastToObject&lt;T&gt;</c> first, then the single-type-parameter <c>Cast&lt;TDestination&gt;</c> overload.</remarks>
    /// <seealso><c>Cast&lt;TDestination&gt;(IObservable&lt;IChangeSet&lt;object&gt;&gt;)</c></seealso>
    /// <seealso><c>Transform&lt;TSource, TDestination&gt;(IObservable&lt;IChangeSet&lt;TSource&gt;&gt;, Func&lt;TSource, TDestination&gt;, bool)</c></seealso>
    public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> conversionFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(conversionFactory);

        return source.Select(changes => changes.Transform(conversionFactory));
    }
}
