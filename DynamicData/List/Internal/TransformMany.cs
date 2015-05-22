using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal class TransformMany<TSource, TDestination>
	{
		private readonly IObservable<IChangeSet<TSource>> _source;
		private readonly Func<TSource, IEnumerable<TDestination>> _manyselector;
		private readonly ChangeAwareList<TDestination> _transformed = new ChangeAwareList<TDestination>();

		public TransformMany([NotNull] IObservable<IChangeSet<TSource>> source, 
			[NotNull]  Func<TSource, IEnumerable<TDestination>> manyselector)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			_source = source;
			_manyselector = manyselector;
		}

		public IObservable<IChangeSet<TDestination>> Run()
		{
			return _source.Select(Process).NotEmpty();
		}

		private IChangeSet<TDestination> Process(IChangeSet<TSource> source)
		{
			//TODO: This is very ineffiecient as it flattens range operation
			//need to find a means of re-forming ranges
			var enumerator = new UnifiedChangeEnumerator<TSource>(source);
			var children = enumerator.SelectMany(change =>
			{
				var many = _manyselector(change.Current);
				return many.Select(m => new TransformedItem<TDestination>(change.Reason, m));
			});

			foreach (var child in children)
			{
				switch (child.Reason)
				{
					case ListChangeReason.Add:
						_transformed.Add(child.Current);
						break;
					case ListChangeReason.Update:
						_transformed.Remove(child.Previous.Value);
						_transformed.Add(child.Current);
						break;
					case ListChangeReason.Remove:
						_transformed.Remove(child.Current);
						break;
                    case ListChangeReason.Clear:
                        _transformed.Clear();
                        break;
                }
			}
			return _transformed.CaptureChanges();
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
				return string.Format("Reason: {0}, Current: {1}, Previous: {2}", Reason, Current, Previous);
			}
		}
	}
}