// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

internal sealed class TransformAsync<TSource, TDestination>
    where TSource : notnull
    where TDestination : notnull
{
    private readonly Func<TSource, Optional<TDestination>, int, CancellationToken, Task<Transformer<TSource, TDestination>.TransformedItemContainer>> _containerFactory;

    private readonly IObservable<IChangeSet<TSource>> _source;
    private readonly bool _transformOnRefresh;

    public TransformAsync(
        IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, int, CancellationToken, Task<TDestination>> factory,
        bool transformOnRefresh)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _transformOnRefresh = transformOnRefresh;
        _containerFactory = async (item, prev, index, cancel) =>
        {
            var destination = await factory(item, prev, index, cancel).ConfigureAwait(false);
            return new Transformer<TSource, TDestination>.TransformedItemContainer(item, destination);
        };
    }

    public IObservable<IChangeSet<TDestination>> Run() => Observable.Defer(RunImpl);

    private IObservable<IChangeSet<TDestination>> RunImpl()
    {
        return Observable.Using(
            () => new SemaphoreSlim(1, 1),
            asyncLock =>
            {
                var state = new ChangeAwareList<Transformer<TSource, TDestination>.TransformedItemContainer>();

                return _source.Select(changes => Observable.FromAsync(async cancel =>
                        {
                            // NOTE: lock outside of the try to avoid releasing another scope's lock if the token is canceled before we acquire the lock.
                            try
                            {
                                await asyncLock.WaitAsync(cancel);
                            }
                            catch (Exception e) when (e is ObjectDisposedException)
                            {
                                return Optional.None<ChangeAwareList<Transformer<TSource, TDestination>.TransformedItemContainer>>();
                            }

                            try
                            {
                                await Transform(state, changes, cancel);
                                return Optional.Some(state);
                            }
                            finally
                            {
                                try
                                {
                                    // token is canceled when outer stream is disposed which is attached to the lock's lifetime.
                                    if (!cancel.IsCancellationRequested)
                                    {
                                        asyncLock.Release();
                                    }
                                }
                                catch (ObjectDisposedException)
                                {
                                    // outer stream was disposed during inner transform.
                                }
                            }
                        }))
                    .Concat()
                    .SelectValues()
                    .Select(transformed =>
                    {
                        var changed = transformed.CaptureChanges();
                        return changed.Transform(container => container.Destination);
                    });
            });
    }

    private async Task Transform(
        ChangeAwareList<Transformer<TSource, TDestination>.TransformedItemContainer> transformed,
        IChangeSet<TSource> changes,
        CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(changes);

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
                                    transformed.Count,
                                    cancel).ConfigureAwait(false);
                            transformed.Add(container);
                        }
                        else
                        {
                            var container =
                                await _containerFactory(
                                    item.Item.Current,
                                    Optional<TDestination>.None,
                                    change.CurrentIndex,
                                    cancel).ConfigureAwait(false);
                            transformed.Insert(change.CurrentIndex, container);
                        }

                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var startIndex = item.Range.Index < 0 ? transformed.Count : item.Range.Index;
                        var tasks = item.Range.Select((t, idx) => _containerFactory(t, Optional<TDestination>.None, idx + startIndex, cancel));
                        var containers = await Task.WhenAll(tasks).ConfigureAwait(false);
                        transformed.AddOrInsertRange(containers, item.Range.Index);
                        break;
                    }

                case ListChangeReason.Refresh:
                    {
                        var change = item.Item;
                        var index = change.CurrentIndex;

                        if (index < 0)
                        {
                            index = transformed.FindItemIndex(change.Current);
                            if (index < 0)
                                break;
                        }

                        if (_transformOnRefresh)
                        {
                            var container = await _containerFactory(change.Current, transformed[index].Destination, index, cancel)
                                .ConfigureAwait(false);
                            transformed[index] = container;
                        }
                        else
                        {
                            transformed.RefreshAt(index);
                        }

                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var change = item.Item;
                        var currentIndex = change.CurrentIndex;
                        var previousIndex = change.PreviousIndex;

                        if (previousIndex < 0)
                            previousIndex = transformed.FindItemIndex(change.Previous.Value);

                        if (currentIndex < 0)
                            currentIndex = previousIndex;

                        if (currentIndex == previousIndex)
                        {
                            transformed[currentIndex] = await _containerFactory(change.Current, transformed[previousIndex].Destination, currentIndex, cancel);
                        }
                        else
                        {
                            transformed.RemoveAt(previousIndex);
                            transformed.Insert(currentIndex, await _containerFactory(change.Current, Optional<TDestination>.None, currentIndex, cancel));
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        var change = item.Item;
                        var index = change.CurrentIndex;

                        if (index < 0)
                        {
                            index = transformed.FindItemIndex(change.Current);
                            if (index < 0)
                                break;
                        }

                        transformed.RemoveAt(index);
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
                            foreach (var current in item.Range)
                            {
                                var index = transformed.FindItemIndex(current);
                                if (index < 0)
                                    continue;

                                transformed.RemoveAt(index);
                            }
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
