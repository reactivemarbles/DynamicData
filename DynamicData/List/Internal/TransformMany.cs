using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;
using DynamicData.List.Internal;

namespace DynamicData.Internal
{
    internal class TransformMany<TSource, TDestination>
    {
        private readonly IObservable<IChangeSet<TSource>> _source;
        private readonly Func<TSource, IEnumerable<TDestination>> _manyselector;


        public TransformMany([NotNull] IObservable<IChangeSet<TSource>> source,
                             [NotNull] Func<TSource, IEnumerable<TDestination>> manyselector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
            _manyselector = manyselector;
        }

        public IObservable<IChangeSet<TDestination>> Run()
        {
            return Observable.Create<IChangeSet<TDestination>>(observer =>
            {
                var transformed = new ChangeAwareList<TDestination>();
                return _source.Select(changes => Process(transformed, changes)).NotEmpty().SubscribeSafe(observer);
            });
        }

        private IChangeSet<TDestination> Process(ChangeAwareList<TDestination> transformed, IChangeSet<TSource> source)
        {
            //TODO: This is very inefficient as it flattens range operation
            //need to find a means of re-forming ranges
            var children = source.Unified().SelectMany(change =>
            {
                var many = _manyselector(change.Current);
                return many.Select(m => new TransformedItem<TDestination>(change.Reason, m));
            });

            foreach (var child in children)
            {
                switch (child.Reason)
                {
                    case ListChangeReason.Add:
                        transformed.Add(child.Current);
                        break;
                    case ListChangeReason.Replace:
                        transformed.Remove(child.Previous.Value);
                        transformed.Add(child.Current);
                        break;
                    case ListChangeReason.Remove:
                        transformed.Remove(child.Current);
                        break;
                    case ListChangeReason.Clear:
                        transformed.Clear();
                        break;
                }
            }
            return transformed.CaptureChanges();
        }

        /// <summary>
        ///  Staging object for ManyTransform.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal struct TransformedItem<T>
        {
            public ListChangeReason Reason { get; }
            public T Current { get; }
            public Optional<T> Previous { get; }

            public TransformedItem(ListChangeReason reason, T current)
                : this(reason, current, Optional.None<T>())
            {
            }

            public TransformedItem(ListChangeReason reason, T current, Optional<T> previous)
            {
                Reason = reason;
                Current = current;
                Previous = previous;
            }

            #region Equality

            public bool Equals(TransformedItem<T> other)
            {
                return Reason == other.Reason && EqualityComparer<T>.Default.Equals(Current, other.Current) &&
                       Previous.Equals(other.Previous);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is TransformedItem<T> && Equals((TransformedItem<T>)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (int)Reason;
                    hashCode = (hashCode * 397) ^ EqualityComparer<T>.Default.GetHashCode(Current);
                    hashCode = (hashCode * 397) ^ Previous.GetHashCode();
                    return hashCode;
                }
            }

            #endregion

            public override string ToString()
            {
                return $"Reason: {Reason}, Current: {Current}, Previous: {Previous}";
            }
        }
    }
}
