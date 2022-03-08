// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Aggregation;

internal class AggregateEnumerator<T> : IAggregateChangeSet<T>
{
    private readonly IChangeSet<T> _source;

    public AggregateEnumerator(IChangeSet<T> source)
    {
        _source = source;
    }

    public IEnumerator<AggregateItem<T>> GetEnumerator()
    {
        foreach (var change in _source)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    yield return new AggregateItem<T>(AggregateType.Add, change.Item.Current);
                    break;

                case ListChangeReason.AddRange:
                    foreach (var item in change.Range)
                    {
                        yield return new AggregateItem<T>(AggregateType.Add, item);
                    }

                    break;

                case ListChangeReason.Replace:
                    yield return new AggregateItem<T>(AggregateType.Remove, change.Item.Previous.Value);
                    yield return new AggregateItem<T>(AggregateType.Add, change.Item.Current);
                    break;

                case ListChangeReason.Remove:
                    yield return new AggregateItem<T>(AggregateType.Remove, change.Item.Current);
                    break;

                case ListChangeReason.RemoveRange:
                case ListChangeReason.Clear:
                    foreach (var item in change.Range)
                    {
                        yield return new AggregateItem<T>(AggregateType.Remove, item);
                    }

                    break;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same name, different generics.")]
internal class AggregateEnumerator<TObject, TKey> : IAggregateChangeSet<TObject>
    where TKey : notnull
{
    private readonly IChangeSet<TObject, TKey> _source;

    public AggregateEnumerator(IChangeSet<TObject, TKey> source)
    {
        _source = source;
    }

    public IEnumerator<AggregateItem<TObject>> GetEnumerator()
    {
        foreach (var change in _source)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    yield return new AggregateItem<TObject>(AggregateType.Add, change.Current);
                    break;

                case ChangeReason.Update:
                    yield return new AggregateItem<TObject>(AggregateType.Remove, change.Previous.Value);
                    yield return new AggregateItem<TObject>(AggregateType.Add, change.Current);
                    break;

                case ChangeReason.Remove:
                    yield return new AggregateItem<TObject>(AggregateType.Remove, change.Current);
                    break;

                default:
                    continue;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
