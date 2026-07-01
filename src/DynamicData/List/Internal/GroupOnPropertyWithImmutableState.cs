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
/// Provides members for the GroupOnPropertyWithImmutableState class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TGroup">The type of the TGroup value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="groupSelectorKey">The groupSelectorKey value.</param>
/// <param name="throttle">The throttle value.</param>
/// <param name="scheduler">The scheduler value.</param>
internal sealed class GroupOnPropertyWithImmutableState<TObject, TGroup>(IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TGroup>> groupSelectorKey, TimeSpan? throttle = null, IScheduler? scheduler = null)
    where TObject : INotifyPropertyChanged
    where TGroup : notnull
{
    /// <summary>
    /// The _groupSelector field.
    /// </summary>
    private readonly Func<TObject, TGroup> _groupSelector = groupSelectorKey.Compile();

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<IGrouping<TObject, TGroup>>> Run() => _source.Publish(
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
                return shared.GroupWithImmutableState(_groupSelector, regrouper);
            });
}
