using System;

namespace DynamicData.Tests
{
    /// <summary>
    /// Test extensions
    /// </summary>
    public static class ListTextEx
    {
        /// <summary>
        /// Aggregates all events and statistics for a changeset to help assertions when testing
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns></returns>
        public static ChangeSetAggregator<T> AsAggregator<T>(this IObservable<IChangeSet<T>> source)
        {
            return new ChangeSetAggregator<T>(source);
        }
    }
}
