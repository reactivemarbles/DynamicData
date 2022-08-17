// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class Transformer<TSource, TDestination>
{
    private readonly Func<TSource, Optional<TDestination>, int, TransformedItemContainer> _containerFactory;

    private readonly IObservable<IChangeSet<TSource>> _source;

    private readonly bool _transformOnRefresh;

    public Transformer(IObservable<IChangeSet<TSource>> source, Func<TSource, Optional<TDestination>, int, TDestination> factory, bool transformOnRefresh)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _transformOnRefresh = transformOnRefresh;
        _containerFactory = (item, prev, index) => new TransformedItemContainer(item, factory(item, prev, index));
    }

    public IObservable<IChangeSet<TDestination>> Run() => Observable.Defer(RunImpl);

    private IObservable<IChangeSet<TDestination>> RunImpl() => _source.Scan(new ChangeAwareList<TransformedItemContainer>(), (state, changes) =>
            {
                Transform(state, changes);
                return state;
            })
            .Select(transformed =>
            {
                var changed = transformed.CaptureChanges();
                return changed.Transform(container => container.Destination);
            });

    private void Transform(ChangeAwareList<TransformedItemContainer> transformed, IChangeSet<TSource> changes)
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
                            transformed.Add(_containerFactory(change.Current, Optional<TDestination>.None, transformed.Count));
                        }
                        else
                        {
                            var converted = _containerFactory(change.Current, Optional<TDestination>.None, change.CurrentIndex);
                            transformed.Insert(change.CurrentIndex, converted);
                        }

                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var startIndex = item.Range.Index < 0 ? transformed.Count : item.Range.Index;

                        transformed.AddOrInsertRange(item.Range.Select((t, idx) => _containerFactory(t, Optional<TDestination>.None, idx + startIndex)), item.Range.Index);

                        break;
                    }

                case ListChangeReason.Refresh:
                    {
                        if (_transformOnRefresh)
                        {
                            var change = item.Item;
                            Optional<TDestination> previous = transformed[change.CurrentIndex].Destination;
                            transformed[change.CurrentIndex] = _containerFactory(change.Current, previous, change.CurrentIndex);
                        }
                        else
                        {
                            transformed.RefreshAt(item.Item.CurrentIndex);
                        }

                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var change = item.Item;
                        Optional<TDestination> previous = transformed[change.PreviousIndex].Destination;
                        if (change.CurrentIndex == change.PreviousIndex)
                        {
                            transformed[change.CurrentIndex] = _containerFactory(change.Current, previous, change.CurrentIndex);
                        }
                        else
                        {
                            transformed.RemoveAt(change.PreviousIndex);
                            transformed.Insert(change.CurrentIndex, _containerFactory(change.Current, Optional<TDestination>.None, change.CurrentIndex));
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        var change = item.Item;
                        bool hasIndex = change.CurrentIndex >= 0;

                        if (hasIndex)
                        {
                            transformed.RemoveAt(change.CurrentIndex);
                        }
                        else
                        {
                            var toRemove = transformed.FirstOrDefault(t => ReferenceEquals(t.Source, change.Current));

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
                            var toRemove = transformed.Where(t => item.Range.Any(current => ReferenceEquals(t.Source, current)));
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

                        transformed.Move(change.PreviousIndex, change.CurrentIndex);
                        break;
                    }
            }
        }
    }

    internal sealed class TransformedItemContainer : IEquatable<TransformedItemContainer>
    {
        public TransformedItemContainer(TSource source, TDestination destination)
        {
            Source = source;
            Destination = destination;
        }

        public TDestination Destination { get; }

        public TSource Source { get; }

        public static bool operator ==(TransformedItemContainer left, TransformedItemContainer right) => Equals(left, right);

        public static bool operator !=(TransformedItemContainer left, TransformedItemContainer right) => !Equals(left, right);

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
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((TransformedItemContainer)obj);
        }

        public override int GetHashCode() => Source is null ? 0 : EqualityComparer<TSource>.Default.GetHashCode(Source);
    }
}
