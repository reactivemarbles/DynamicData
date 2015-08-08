using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// Change set extensions
	/// </summary>
	public static class ChangeSetEx
	{
        /// <summary>
        /// Gets the type of the change i.e. whether it is an item or a range change
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public static ChangeType GetChangeType(this ListChangeReason source)
        {
                switch (source)
                {
                    case ListChangeReason.Add:
                        return ChangeType.Item;
                    case ListChangeReason.AddRange:
                        return ChangeType.Range;
                    case ListChangeReason.Replace:
                        return ChangeType.Item;
                    case ListChangeReason.Remove:
                        return ChangeType.Item;
                    case ListChangeReason.RemoveRange:
                        return ChangeType.Range;
                    case ListChangeReason.Moved:
                        return ChangeType.Item;
                    case ListChangeReason.Clear:
                        return ChangeType.Range;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        

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


    }
}