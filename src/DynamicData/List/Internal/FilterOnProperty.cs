// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the FilterOnProperty class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TProperty">The type of the TProperty value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="propertySelector">The propertySelector value.</param>
/// <param name="predicate">The predicate value.</param>
/// <param name="throttle">The throttle value.</param>
/// <param name="scheduler">The scheduler value.</param>
[Obsolete("Use AutoRefresh(), followed by Filter() instead")]
internal sealed class FilterOnProperty<TObject, TProperty>(IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? throttle = null, IScheduler? scheduler = null)
    where TObject : INotifyPropertyChanged
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject>> Run() => source.AutoRefresh(propertySelector, propertyChangeThrottle: throttle, scheduler: scheduler).Filter(predicate);
}
