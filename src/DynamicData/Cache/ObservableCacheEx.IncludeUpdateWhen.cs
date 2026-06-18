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
    /// <summary>
    /// Only includes the update when the condition is met.
    /// The first parameter in the ignore function is the current value and the second parameter is the previous value.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to selectively include updates in.</param>
    /// <param name="includeFunction">The <see cref="Func{TObject, TObject, bool}"/> include function (current,previous)=>{ return true to include }.</param>
    /// <returns>An observable which emits change sets and ignores updates equal to the lambda.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> IncludeUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject, bool> includeFunction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        includeFunction.ThrowArgumentNullExceptionIfNull(nameof(includeFunction));

        return source.Select(
            changes =>
            {
                var result = changes.Where(change => change.Reason != ChangeReason.Update || includeFunction(change.Current, change.Previous.Value));
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();
    }
}
