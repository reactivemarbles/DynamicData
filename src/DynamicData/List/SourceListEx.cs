using System;

using DynamicData.Annotations;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Source list extensions
    /// </summary>
    public static class SourceListEx
    {
        /// <summary>
        /// Connects to the list, and converts the changes to another form
        /// 
        /// Alas, I had to add the converter due to type inference issues 
        /// </summary>
        /// <typeparam name="TSource">The type of the object.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="conversionFactory">The conversion factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>([NotNull] this ISourceList<TSource> source,
                                                                                           [NotNull] Func<TSource, TDestination> conversionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (conversionFactory == null) throw new ArgumentNullException(nameof(conversionFactory));
            return source.Connect().Cast(conversionFactory);
        }
    }
}
