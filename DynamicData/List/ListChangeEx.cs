using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData
{
	public static class ListChangeEx
	{

		/// <summary>
		/// Filters the source from the changes, using the specified predicate
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="changes">The changes.</param>
		/// <param name="predicate">The predicate.</param>
		internal static void Filter<T>(this IList<T> source, IChangeSet<T> changes, Func<T, bool> predicate)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (changes == null) throw new ArgumentNullException("changes");
			if (predicate == null) throw new ArgumentNullException("predicate");

			changes.ForEach(item =>
			{

				switch (item.Reason)
				{
					case ListChangeReason.Add:
					{
						var change = item.Item;
						var match = predicate(change.Current);
						if (match) source.Add(change.Current);
					}
						break;
					case ListChangeReason.AddRange:
					{
						var matches = item.Range.Where(predicate).ToList();
						source.AddRange(matches);
					}
						break;


					case ListChangeReason.Update:
					{
						var change = item.Item;
						var match = predicate(change.Current);
						var wasMatch = predicate(change.Previous.Value);

						if (match)
						{
							if (wasMatch)
							{
								//an update, so get the latest index
								var previous = source.Lookup(change.Previous.Value, ReferenceEqualityComparer<T>.Instance)
									.ValueOrThrow(() => new InvalidOperationException("Cannot find item. Expected to be in the list"));

								//replace inline
								source[previous.Index] = change.Current;
							}
							else
							{
								source.Add(change.Current);
							}
						}
						else
						{
							if (wasMatch)
								source.Remove(change.Previous.Value);
						}
					}

						break;
					case ListChangeReason.Remove:
					{
						var change = item.Item;
						var wasMatch = predicate(change.Current);
						if (wasMatch) source.Remove(change.Current);
					}
						break;

					case ListChangeReason.RemoveRange:
						{
							//can this be optimised? should we check whether it was a match?
							item.Range.ForEach(t=> source.Remove(t));
						}
						break;

					case ListChangeReason.Clear:
						{
							source.Clear();
						}
						break;
				}
			});


		}

		/// <summary>
		/// Clones the source list with the specified change set, transforming the items using the specified factory
		/// </summary>
		/// <typeparam name="TSource">The type of the source.</typeparam>
		/// <typeparam name="TDestination">The type of the destination.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="changes">The changes.</param>
		/// <param name="transformFactory">The transform factory.</param>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// changes
		/// or
		/// transformFactory
		/// </exception>
		public static void Transform<TSource, TDestination>(this IList<TDestination> source, IChangeSet<TSource> changes, Func<TSource, TDestination> transformFactory)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (changes == null) throw new ArgumentNullException("changes");
			if (transformFactory == null) throw new ArgumentNullException("transformFactory");

			source.EnsureCapacityFor(changes);
			changes.ForEach(item =>
			{
				switch (item.Reason)
				{
					case ListChangeReason.Add:
					{
						var change = item.Item;
						source.Insert(change.CurrentIndex, transformFactory(change.Current));
						break;
					}
					case ListChangeReason.AddRange:
						{
							source.AddOrInsertRange(item.Range.Select(transformFactory), item.Range.Index);
							break;
						}
					case ListChangeReason.Update:
					{
						var change = item.Item;
						if (change.CurrentIndex == change.PreviousIndex)
						{
							source[change.CurrentIndex] = transformFactory(change.Current);
						}
						else
						{
							source.RemoveAt(change.PreviousIndex);
							source.Insert(change.CurrentIndex, transformFactory(change.Current));
						}
					}
						break;
					case ListChangeReason.Remove:
					{
						var change = item.Item;
						source.RemoveAt(change.CurrentIndex);
					}
						break;
					case ListChangeReason.RemoveRange:
						{
							source.RemoveRange(item.Range.Index, item.Range.Count);
						}
						break;
					case ListChangeReason.Clear:
						{
							source.Clear();
						}
						break;
				}
			});

		}

		/// <summary>
		/// Clones the list from the specified change set
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="changes">The changes.</param>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// changes
		/// </exception>
		public static void Clone<T>(this IList<T> source, IChangeSet<T> changes)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (changes == null) throw new ArgumentNullException("changes");

			changes.ForEach(item =>
			{
		
				switch (item.Reason)
				{
					case ListChangeReason.Add:
					{
						var change = item.Item;
						bool hasIndex = change.CurrentIndex >= 0;
						if (hasIndex)
						{
							source.Insert(change.CurrentIndex, change.Current);
						}
						else
						{
							source.Add(change.Current);
						}
						break;
					}

					case ListChangeReason.AddRange:
					{
							source.AddOrInsertRange(item.Range, item.Range.Index);
							break;
						}

					case ListChangeReason.Clear:
						{
							source.Clear();
							break;
						}

					case ListChangeReason.Update:
						{

							var change = item.Item;
							bool hasIndex = change.CurrentIndex >= 0;
							if (hasIndex && change.CurrentIndex == change.PreviousIndex)
							{
								source[change.CurrentIndex] = change.Current;
							}
							else
							{
								//is this best? or replace + move?
								source.RemoveAt(change.PreviousIndex);
								source.Insert(change.CurrentIndex, change.Current);
							}
						
						}
						break;
					case ListChangeReason.Remove:
						{

							var change = item.Item;
							bool hasIndex = change.CurrentIndex >= 0;
							if (hasIndex)
							{
								source.RemoveAt(change.CurrentIndex);
							}
							else
							{
								source.Remove(change.Current);
							}

							break;
						}

					case ListChangeReason.RemoveRange:
						{
							source.RemoveRange(item.Range.Index, item.Range.Count);
							break;
						}
					case ListChangeReason.Moved:
						{
							var change = item.Item;
							bool hasIndex = change.CurrentIndex >= 0;
							if (!hasIndex)
								throw new UnspecifiedIndexException("Cannot move as an index was not specified");

							var collection = source as ChangeAwareList<T>;
							if (collection != null)
							{
								collection.Move(change.PreviousIndex, change.CurrentIndex);
							}
							else
							{
								//check this works whether the index is 
								source.RemoveAt(change.PreviousIndex);
								source.Insert(change.CurrentIndex, change.Current);
							}

							break;
						}
				}
			});


		}

	}
}