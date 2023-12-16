// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class TransformAsync<TSource, TDestination>
    where TSource : notnull
    where TDestination : notnull
{
    private readonly Func<TSource, Optional<TDestination>, int, Task<Transformer<TSource, TDestination>.TransformedItemContainer>> _containerFactory;

    private readonly IObservable<IChangeSet<TSource>> _source;
    private readonly bool _transformOnRefresh;

    public TransformAsync(
        IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, int, Task<TDestination>> factory,
        bool transformOnRefresh)
    {
        factory.ThrowArgumentNullExceptionIfNull(nameof(factory));

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _transformOnRefresh = transformOnRefresh;
        _containerFactory = async (item, prev, index) =>
        {
            var destination = await factory(item, prev, index).ConfigureAwait(false);
            return new Transformer<TSource, TDestination>.TransformedItemContainer(item, destination);
        };
    }

    public IObservable<IChangeSet<TDestination>> Run() => Observable.Defer(RunImpl);

    private IObservable<IChangeSet<TDestination>> RunImpl()
    {
        var state = new ChangeAwareList<Transformer<TSource, TDestination>.TransformedItemContainer>();
        var asyncLock = new SemaphoreSlim(1, 1);

        return _source.Select(async changes =>
                {
                    try
                    {
                        await asyncLock.WaitAsync();
                        await Transform(state, changes);
                        return state;
                    }
                    finally
                    {
                        asyncLock.Release();
                    }
                })
            .Concat()
            .Select(transformed =>
            {
                var changed = transformed.CaptureChanges();
                return changed.Transform(container => container.Destination);
            });
    }

    private async Task Transform(
        ChangeAwareList<Transformer<TSource, TDestination>.TransformedItemContainer> transformed,
        IChangeSet<TSource> changes)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        foreach (var item in changes)
        {
            switch (item.Reason)
            {
                case ListChangeReason.Add:
                    {
                        var change = item.Item;
                        if (change.CurrentIndex < 0 || change.CurrentIndex >= transformed.Count)
                        {
                            var container =
                                await _containerFactory(
                                    item.Item.Current,
                                    Optional<TDestination>.None,
                                    transformed.Count).ConfigureAwait(false);
                            transformed.Add(container);
                        }
                        else
                        {
                            var container =
                                await _containerFactory(
                                    item.Item.Current,
                                    Optional<TDestination>.None,
                                    change.CurrentIndex).ConfigureAwait(false);
                            transformed.Insert(change.CurrentIndex, container);
                        }

                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var startIndex = item.Range.Index < 0 ? transformed.Count : item.Range.Index;
                        var tasks = item.Range.Select((t, idx) => _containerFactory(t, Optional<TDestination>.None, idx + startIndex));
                        var containers = await Task.WhenAll(tasks).ConfigureAwait(false);
                        transformed.AddOrInsertRange(containers, item.Range.Index);
                        break;
                    }

                case ListChangeReason.Refresh:
                    {
                        var change = item.Item;
                        if (_transformOnRefresh)
                        {
                            Optional<TDestination> previous = transformed[change.CurrentIndex].Destination;
                            var container = await _containerFactory(change.Current, previous, change.CurrentIndex)
                                .ConfigureAwait(false);
                            transformed[change.CurrentIndex] = container;
                        }
                        else
                        {
                            transformed.RefreshAt(change.CurrentIndex);
                        }

                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var change = item.Item;

                        Optional<TDestination> previous = transformed[change.PreviousIndex].Destination;
                        if (change.CurrentIndex == change.PreviousIndex)
                        {
                            transformed[change.CurrentIndex] = await _containerFactory(change.Current, previous, change.CurrentIndex);
                        }
                        else
                        {
                            transformed.RemoveAt(change.PreviousIndex);
                            transformed.Insert(change.CurrentIndex, await _containerFactory(change.Current, Optional<TDestination>.None, change.CurrentIndex));
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        var change = item.Item;
                        var hasIndex = change.CurrentIndex >= 0;

                        if (hasIndex)
                        {
                            transformed.RemoveAt(item.Item.CurrentIndex);
                        }
                        else
                        {
                            var toRemove = transformed.FirstOrDefault(t => ReferenceEquals(t.Source, t));

                            if (toRemove is not null)
                            {
                                transformed.Remove(toRemove);
                            }
                        }

                        break;
                    }

                case ListChangeReason.RemoveRange:
                    {
                        if (item.Range.Index >= 0)
                        {
                            transformed.RemoveRange(item.Range.Index, item.Range.Count);
                        }
                        else
                        {
                            var toRemove = transformed.Where(t => ReferenceEquals(t.Source, t)).ToArray();
                            transformed.RemoveMany(toRemove);
                        }

                        break;
                    }

                case ListChangeReason.Clear:
                    {
                        // i.e. need to store transformed reference so we can correctly clear
                        var toClear =
                            new Change<Transformer<TSource, TDestination>.TransformedItemContainer>(
                                ListChangeReason.Clear, transformed);
                        transformed.ClearOrRemoveMany(toClear);

                        break;
                    }

                case ListChangeReason.Moved:
                    {
                        var change = item.Item;
                        var hasIndex = change.CurrentIndex >= 0;
                        if (!hasIndex)
                        {
                            throw new UnspecifiedIndexException("Cannot move as an index was not specified");
                        }

                        if (transformed is IExtendedList<Transformer<TSource, TDestination>.TransformedItemContainer>
                            collection)
                        {
                            collection.Move(change.PreviousIndex, change.CurrentIndex);
                        }
                        else
                        {
                            var current = transformed[change.PreviousIndex];
                            transformed.RemoveAt(change.PreviousIndex);
                            transformed.Insert(change.CurrentIndex, current);
                        }

                        break;
                    }
            }
        }
    }
}
