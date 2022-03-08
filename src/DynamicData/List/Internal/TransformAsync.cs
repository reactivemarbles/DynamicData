// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace DynamicData.List.Internal;

internal class TransformAsync<TSource, TDestination>
{
    private readonly Func<TSource, Task<TransformedItemContainer>> _containerFactory;

    private readonly IObservable<IChangeSet<TSource>> _source;

    public TransformAsync(IObservable<IChangeSet<TSource>> source, Func<TSource, Task<TDestination>> factory)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _containerFactory = async item =>
        {
            var destination = await factory(item).ConfigureAwait(false);
            return new TransformedItemContainer(item, destination);
        };
    }

    public IObservable<IChangeSet<TDestination>> Run()
    {
        return Observable.Create<IChangeSet<TDestination>>(
            observer =>
            {
                var state = new ChangeAwareList<TransformedItemContainer>();

                return _source.Select(
                    async changes =>
                    {
                        await Transform(state, changes).ConfigureAwait(false);
                        return state;
                    }).Select(tasks => tasks.ToObservable()).SelectMany(items => items).Select(
                    transformed =>
                    {
                        var changed = transformed.CaptureChanges();
                        return changed.Transform(container => container.Destination);
                    }).SubscribeSafe(observer);
            });
    }

    private async Task Transform(ChangeAwareList<TransformedItemContainer> transformed, IChangeSet<TSource> changes)
    {
        if (changes is null)
        {
            throw new ArgumentNullException(nameof(changes));
        }

        foreach (var item in changes)
        {
            switch (item.Reason)
            {
                case ListChangeReason.Add:
                    {
                        var change = item.Item;
                        if (change.CurrentIndex < 0 | change.CurrentIndex >= transformed.Count)
                        {
                            var container = await _containerFactory(item.Item.Current).ConfigureAwait(false);
                            transformed.Add(container);
                        }
                        else
                        {
                            var container = await _containerFactory(item.Item.Current).ConfigureAwait(false);
                            transformed.Insert(change.CurrentIndex, container);
                        }

                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var tasks = item.Range.Select(_containerFactory);
                        var containers = await Task.WhenAll(tasks).ConfigureAwait(false);
                        transformed.AddOrInsertRange(containers, item.Range.Index);
                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var change = item.Item;
                        var container = await _containerFactory(item.Item.Current).ConfigureAwait(false);

                        if (change.CurrentIndex == change.PreviousIndex)
                        {
                            transformed[change.CurrentIndex] = container;
                        }
                        else
                        {
                            transformed.RemoveAt(change.PreviousIndex);
                            transformed.Insert(change.CurrentIndex, container);
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        var change = item.Item;
                        bool hasIndex = change.CurrentIndex >= 0;

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
                        var toClear = new Change<TransformedItemContainer>(ListChangeReason.Clear, transformed);
                        transformed.ClearOrRemoveMany(toClear);

                        break;
                    }

                case ListChangeReason.Moved:
                    {
                        var change = item.Item;
                        bool hasIndex = change.CurrentIndex >= 0;
                        if (!hasIndex)
                        {
                            throw new UnspecifiedIndexException("Cannot move as an index was not specified");
                        }

                        if (transformed is IExtendedList<TransformedItemContainer> collection)
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

    private class TransformedItemContainer : IEquatable<TransformedItemContainer>
    {
        public TransformedItemContainer(TSource source, TDestination destination)
        {
            Source = source;
            Destination = destination;
        }

        public TDestination Destination { get; }

        public TSource Source { get; }

        public static bool operator ==(TransformedItemContainer left, TransformedItemContainer right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TransformedItemContainer left, TransformedItemContainer right)
        {
            return !Equals(left, right);
        }

        public bool Equals(TransformedItemContainer? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<TSource>.Default.Equals(Source, other.Source);
        }

        public override bool Equals(object? obj)
        {
            return obj is TransformedItemContainer item && Equals(item);
        }

        public override int GetHashCode()
        {
            return Source is null ? 0 : EqualityComparer<TSource>.Default.GetHashCode(Source);
        }
    }
}
