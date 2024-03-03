// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOnProperty<TObject, TKey, TGroup>(IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TGroup>> groupSelectorKey, TimeSpan? throttle = null, IScheduler? scheduler = null)
    where TObject : INotifyPropertyChanged
    where TKey : notnull
    where TGroup : notnull
{
    private readonly Func<TObject, TGroup> _groupSelector = groupSelectorKey.Compile();
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IGroupChangeSet<TObject, TKey, TGroup>> Run() => _source.Publish(
            shared =>
            {
                // Monitor explicit property changes
                var regrouper = shared.WhenValueChanged(groupSelectorKey, false).ToUnit();

                // add a throttle if specified
                if (throttle is not null)
                {
                    regrouper = regrouper.Throttle(throttle.Value, scheduler ?? GlobalConfig.DefaultScheduler);
                }

                // Use property changes as a trigger to re-evaluate Grouping
                return shared.Group(_groupSelector, regrouper);
            });
}
