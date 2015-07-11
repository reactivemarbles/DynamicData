using System.Collections;
using System.Collections.Generic;

namespace DynamicData.Internal
{

    internal static class WithoutIndexEnumeratorEx
    {
        /// <summary>
        /// Remove the index from the changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IEnumerable<Change<T>> YieldWithoutIndex<T>(this IEnumerable<Change<T>>  source)
        {
            return new WithoutIndexEnumerator<T>(source);
        }

    }


    /// <summary>
    /// Index to remove the index. This is necessary for WhereReasonAre* operators. 
    /// Otherwise these operators could break subsequent operators when the subsequent operator relies on the index
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class WithoutIndexEnumerator<T> : IEnumerable<Change<T>>
    {
        private readonly IEnumerable<Change<T>> _changeSet;

        public WithoutIndexEnumerator(IEnumerable<Change<T>> changeSet)
        {
            _changeSet = changeSet;
        }


        public IEnumerator<Change<T>> GetEnumerator()
        {
            foreach (var change in _changeSet)
            {
                if (change.Reason == ListChangeReason.Moved)
                {
                    //exceptional case - makes no sense to remove index from move 
                    //TODO: Consider whether Moves should be  ignored
                    yield return change;
                    continue;
                }

                if (change.Type == ChangeType.Item)
                {
                    yield return new Change<T>(change.Reason, change.Item.Current, change.Item.Previous);
                }
                else
                {
                    yield return new Change<T>(change.Reason, change.Range);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}