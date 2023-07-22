// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal class FilterStatic<T>
    where T : notnull
{
    private readonly Func<T, bool> _predicate;

    private readonly IObservable<IChangeSet<T>> _source;

    public FilterStatic(IObservable<IChangeSet<T>> source, Func<T, bool> predicate)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public IObservable<IChangeSet<T>> Run()
    {
        return Observable.Defer(() =>
        {
            return _source.Scan(
                new ChangeAwareList<T>(),
                (state, changes) =>
                {
                    Process(state, changes);
                    return state;
                }).Select(filtered => filtered.CaptureChanges()).NotEmpty();
        });
    }

    private void Process(ChangeAwareList<T> filtered, IChangeSet<T> changes)
    {
        foreach (var item in changes)
        {
            switch (item.Reason)
            {
                case ListChangeReason.Add:
                    {
                        var change = item.Item;
                        if (_predicate(change.Current))
                        {
                            filtered.Add(change.Current);
                        }

                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var matches = item.Range.Where(t => _predicate(t)).ToList();
                        filtered.AddRange(matches);
                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var change = item.Item;
                        var match = _predicate(change.Current);
                        if (match)
                        {
                            filtered.ReplaceOrAdd(change.Previous.Value, change.Current);
                        }
                        else
                        {
                            filtered.Remove(change.Previous.Value);
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        filtered.Remove(item.Item.Current);
                        break;
                    }

                case ListChangeReason.RemoveRange:
                    {
                        filtered.RemoveMany(item.Range);
                        break;
                    }

                case ListChangeReason.Clear:
                    {
                        filtered.ClearOrRemoveMany(item);
                        break;
                    }
            }
        }
    }
}
