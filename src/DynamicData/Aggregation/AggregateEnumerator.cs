// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using DynamicData.Cache;

namespace DynamicData.Aggregation;

internal sealed class AggregateEnumerator<T>(IChangeSet<T> source) : IAggregateChangeSet<T>
    where T : notnull
{
    public IEnumerator<AggregateItem<T>> GetEnumerator()
    {
        foreach (var change in source)
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

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same name, different generics.")]
internal sealed class AggregateEnumerator<TObject, TKey>(IChangeSet<TObject, TKey> source) : IAggregateChangeSet<TObject>
    where TObject : notnull
    where TKey : notnull
{
    private readonly ChangeSet<TObject, TKey> _source = source.ToConcreteType();

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

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
