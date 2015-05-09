using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// Change set extensions
	/// </summary>
	public static class ChangeSetEx
	{
		/// <summary>
		/// Transforms the changeset into a different type using the specified transform function.
		/// </summary>
		/// <typeparam name="TSource">The type of the source.</typeparam>
		/// <typeparam name="TDestination">The type of the destination.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="transformer">The transformer.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException">
		/// source
		/// or
		/// transformer
		/// </exception>
		public static IChangeSet<TDestination> Transform<TSource, TDestination>([NotNull] this IChangeSet<TSource> source,
			[NotNull] Func<TSource, TDestination>  transformer)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (transformer == null) throw new ArgumentNullException(nameof(transformer));

			var changes = source.Select(change =>
			{
				if (change.Type == ChangeType.Item)
				{
					return new Change<TDestination>(change.Reason,
						transformer(change.Item.Current),
						change.Item.Previous.Convert(transformer),
						change.Item.CurrentIndex,
						change.Item.PreviousIndex);
				}
				return new Change<TDestination>(change.Reason, change.Range.Select(transformer), change.Range.Index);


			});

			return new ChangeSet<TDestination>(changes);
		}

		/// <summary>
		/// Returns a flattend source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException">source</exception>
		internal static IEnumerable<UnifiedChange<T>> Unified<T>([NotNull] this IChangeSet<T> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			return new UnifiedChangeEnumerator<T>(source);
		}

        //internal static  IChangeSet<T> Optimise<T>([NotNull] this IEnumerable<UnifiedChange<T>> source)
        //{
        //    var items = new List<Change<T>>();


        //    Change<T> previous=null;

        //    source.ForEach(change =>
        //    {
        //        if (previous == null)
        //        {
        //            items.Add(new Change<T>(change.Reason,change.Current,change.Previous));
        //        }
        //        else
        //        {
        //            if (previous.Reason == ListChangeReason.Add && change.Reason== ListChangeReason.Add)
        //            {
        //                //begin a new batch
        //                var firstOfBatch = items.Count - 1;
        //                items[firstOfBatch] = new Change<T>(ListChangeReason.AddRange, new[] { previous.Current, item }, previousItem.CurrentIndex);

        //            }

        //        }


        //        var current =

        //        if (isFirst)


        //            previous = change;

        //    });





        //    return new ChangeSet<T>(items);
        //}
    }
}