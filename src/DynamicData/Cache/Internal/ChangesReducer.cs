using System.Diagnostics.Contracts;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal static class ChangesReducer
    {
        [Pure]
        public static Optional<Change<TObject, TKey>> Reduce<TObject, TKey>(Optional<Change<TObject, TKey>> previous, Change<TObject, TKey> next)
        {
            if (!previous.HasValue) return next;
            var previousValue = previous.Value;

            switch (previousValue.Reason)
            {
                case ChangeReason.Add when next.Reason == ChangeReason.Remove:
                    return Optional<Change<TObject, TKey>>.None;

                case ChangeReason.Remove when next.Reason == ChangeReason.Add:
                    return new Change<TObject, TKey>(ChangeReason.Update, next.Key, next.Current, previousValue.Current, next.CurrentIndex, previousValue.CurrentIndex);

                case ChangeReason.Add when next.Reason == ChangeReason.Update:
                    return new Change<TObject, TKey>(ChangeReason.Add, next.Key, next.Current, next.CurrentIndex);

                case ChangeReason.Update when next.Reason == ChangeReason.Update:
                    return new Change<TObject, TKey>(ChangeReason.Update, previousValue.Key, next.Current, previousValue.Previous, next.CurrentIndex, previousValue.PreviousIndex);

                default:
                    return next;
            }
        }
    }
}
