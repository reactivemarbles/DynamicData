// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector,
        IEqualityComparer<TDestination>? equalityComparer,
        IComparer<TDestination>? comparer)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() =>
        source.OrchestrateManyMerged(changeSetSelector, equalityComparer, comparer);
}
