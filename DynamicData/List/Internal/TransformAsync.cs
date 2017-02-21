using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class TransformAsync<TSource, TDestination>
    {
        private readonly IObservable<IChangeSet<TSource>> _source;
        private readonly Func<TSource, Task<TransformedItemContainer>> _containerFactory;

        public TransformAsync([NotNull] IObservable<IChangeSet<TSource>> source, [NotNull] Func<TSource, Task<TDestination>> factory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _source = source;
            _containerFactory = async item =>
            {
                var destination = await factory(item);
                return new TransformedItemContainer(item, destination);
            };
        }

        public IObservable<IChangeSet<TDestination>> Run()
        {
            return Observable.Create<IChangeSet<TDestination>>(observer =>
            {
                var state = new ChangeAwareList<TransformedItemContainer>();

                var subscriber = _source.Select(async changes =>
                    {
                        await Transform(state, changes);
                        return state;
                    })
                    .Select(tasks => tasks.ToObservable())
                    .SelectMany(items => items)
                    .Select(transformed =>
                    {
                        var changed = transformed.CaptureChanges();
                        return changed.Transform(container => container.Destination);
                    }).SubscribeSafe(observer);

                return subscriber;
            });
        }



        private async Task Transform(ChangeAwareList<TransformedItemContainer> transformed, IChangeSet<TSource> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            transformed.EnsureCapacityFor(changes);

            foreach (var item in changes)
            {
                switch (item.Reason)
                {
                    case ListChangeReason.Add:
                    {
                        var change = item.Item;
                        if (change.CurrentIndex < 0 | change.CurrentIndex >= transformed.Count)
                        {
                            var container = await _containerFactory(item.Item.Current);
                            transformed.Add(container);
                        }
                        else
                        {
                            var container = await _containerFactory(item.Item.Current);
                            transformed.Insert(change.CurrentIndex, container);
                        }
                        break;
                    }
                    case ListChangeReason.AddRange:
                    {
                        var tasks = item.Range.Select(_containerFactory);
                        var containers = await Task.WhenAll(tasks);
                        transformed.AddOrInsertRange(containers, item.Range.Index);
                        break;
                    }
                    case ListChangeReason.Replace:
                    {
                        var change = item.Item;
                        var container = await _containerFactory(item.Item.Current);

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
                            var toremove = transformed.FirstOrDefault(t => ReferenceEquals(t.Source, t));

                            if (toremove != null)
                                transformed.Remove(toremove);
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
                            var toremove = transformed.Where(t => ReferenceEquals(t.Source, t)).ToArray();
                            transformed.RemoveMany(toremove);
                        }

                        break;
                    }
                    case ListChangeReason.Clear:
                    {
                        //i.e. need to store transformed reference so we can correctly clear
                        var toClear = new Change<TransformedItemContainer>(ListChangeReason.Clear, transformed);
                        transformed.ClearOrRemoveMany(toClear);

                        break;
                    }
                    case ListChangeReason.Moved:
                    {
                        var change = item.Item;
                        bool hasIndex = change.CurrentIndex >= 0;
                        if (!hasIndex)
                            throw new UnspecifiedIndexException("Cannot move as an index was not specified");

                        var collection = transformed as IExtendedList<TransformedItemContainer>;
                        if (collection != null)
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
            public TSource Source { get; }
            public TDestination Destination { get; }

            public TransformedItemContainer(TSource source, TDestination destination)
            {
                Source = source;
                Destination = destination;
            }

            #region Equality

            public bool Equals(TransformedItemContainer other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return EqualityComparer<TSource>.Default.Equals(Source, other.Source);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TransformedItemContainer)obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<TSource>.Default.GetHashCode(Source);
            }

            public static bool operator ==(TransformedItemContainer left, TransformedItemContainer right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(TransformedItemContainer left, TransformedItemContainer right)
            {
                return !Equals(left, right);
            }

            #endregion
        }
    }
}