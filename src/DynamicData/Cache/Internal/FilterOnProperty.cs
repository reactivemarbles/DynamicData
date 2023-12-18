// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Concurrency;

namespace DynamicData.Cache.Internal;

[Obsolete("Use AutoRefresh(), followed by Filter() instead")]
internal sealed class FilterOnProperty<TObject, TKey, TProperty>(IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? throttle = null, IScheduler? scheduler = null)
    where TObject : INotifyPropertyChanged
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() => source.AutoRefresh(propertySelector, propertyChangeThrottle: throttle, scheduler: scheduler).Filter(predicate);
}
