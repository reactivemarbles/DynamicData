// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if WINUI3UWP
using Microsoft.UI.Xaml.Data;
#else
using System.ComponentModel;
#endif
using System.Linq.Expressions;
using System.Reactive.Concurrency;

namespace DynamicData.Cache.Internal
{
    [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
    internal class FilterOnProperty<TObject, TKey, TProperty>
        where TKey : notnull
        where TObject : INotifyPropertyChanged
    {
        private readonly Func<TObject, bool> _predicate;

        private readonly Expression<Func<TObject, TProperty>> _propertySelector;

        private readonly IScheduler? _scheduler;

        private readonly IObservable<IChangeSet<TObject, TKey>> _source;

        private readonly TimeSpan? _throttle;

        public FilterOnProperty(IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? throttle = null, IScheduler? scheduler = null)
        {
            _source = source;
            _propertySelector = propertySelector;
            _predicate = predicate;
            _throttle = throttle;
            _scheduler = scheduler;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return _source.AutoRefresh(_propertySelector, propertyChangeThrottle: _throttle, scheduler: _scheduler).Filter(_predicate);
        }
    }
}