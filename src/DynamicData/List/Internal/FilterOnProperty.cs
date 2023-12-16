// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Concurrency;

namespace DynamicData.List.Internal;

[Obsolete("Use AutoRefresh(), followed by Filter() instead")]
internal sealed class FilterOnProperty<TObject, TProperty>(IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? throttle = null, IScheduler? scheduler = null)
    where TObject : INotifyPropertyChanged
{
    public IObservable<IChangeSet<TObject>> Run() => source.AutoRefresh(propertySelector, propertyChangeThrottle: throttle, scheduler: scheduler).Filter(predicate);
}
