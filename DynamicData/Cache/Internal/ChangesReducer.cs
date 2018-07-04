using System.Diagnostics.Contracts;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class ChangesReducer
    {
        [Pure]
        public static Optional<Change<TObject, TKey>> Reduce<TObject, TKey>(
            Optional<Change<TObject, TKey>> previous,
            Change<TObject, TKey> next)
        {
            if (!previous.HasValue) return next;
            var previousValue = previous.Value;

            if (previousValue.Reason == ChangeReason.Add && next.Reason == ChangeReason.Remove)
            {
                return Optional<Change<TObject, TKey>>.None;
            } 
            else if (previousValue.Reason == ChangeReason.Remove && next.Reason == ChangeReason.Add)
            {
                return Optional.Some(
                    new Change<TObject, TKey>(ChangeReason.Update, next.Key, next.Current, previousValue.Current,
                        next.CurrentIndex, previousValue.CurrentIndex)
                );
            }
            else if (previousValue.Reason == ChangeReason.Add && next.Reason == ChangeReason.Update)
            {
                return Optional.Some(new Change<TObject, TKey>(ChangeReason.Add, next.Key, next.Current, next.CurrentIndex));
            }
            else if (previousValue.Reason == ChangeReason.Update && next.Reason == ChangeReason.Update)
            {
                return Optional.Some(
                   new Change<TObject, TKey>(ChangeReason.Update, previousValue.Key, next.Current, previousValue.Previous,
                        next.CurrentIndex, previousValue.PreviousIndex)
                );
            }
            else
            {
                return next;
            }
        }
    }
}
