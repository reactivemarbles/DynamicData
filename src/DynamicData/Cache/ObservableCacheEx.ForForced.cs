// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    private static IObservable<Func<TSource, TKey, bool>>? ForForced<TSource, TKey>(this IObservable<Unit>? source)
        where TKey : notnull => source?.Select(
            _ =>
            {
                static bool Transformer(TSource item, TKey key) => true;
                return (Func<TSource, TKey, bool>)Transformer;
            });

    private static IObservable<Func<TSource, TKey, bool>>? ForForced<TSource, TKey>(this IObservable<Func<TSource, bool>>? source)
        where TKey : notnull => source?.Select(
            condition =>
            {
                bool Transformer(TSource item, TKey key) => condition(item);
                return (Func<TSource, TKey, bool>)Transformer;
            });
}
