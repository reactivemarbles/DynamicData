// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

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
    ///     Selects distinct values from the source.
    /// </summary>
    /// <typeparam name="TObject">The type object from which the distinct values are selected.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to extract distinct values.</param>
    /// <param name="valueSelector">The <c>Func&lt;TObject, TValue&gt;</c> value selector.</param>
    /// <returns>An observable which will emit distinct change sets.</returns>
    /// <remarks>
    /// Due to it's nature only adds or removes can be returned.
    /// <para><b>Worth noting:</b> Reference counting assumes value equality is transitive. Mutable value objects with inconsistent <c>Equals</c> implementations can corrupt ref counts.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <seealso><c>ObservableListEx.DistinctValues</c></seealso>
    public static IObservable<IDistinctChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return Observable.Create<IDistinctChangeSet<TValue>>(observer => new DistinctCalculator<TObject, TKey, TValue>(source, valueSelector).Run().SubscribeSafe(observer));
    }
}
