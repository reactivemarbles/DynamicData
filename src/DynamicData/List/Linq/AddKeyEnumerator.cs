using System;
using System.Collections;
using System.Collections.Generic;
using DynamicData.Annotations;

namespace DynamicData.List.Linq
{
    internal class AddKeyEnumerator<TObject, TKey> : IEnumerable<Change<TObject, TKey>>
    {
        private readonly IChangeSet<TObject> _source;
        private readonly Func<TObject, TKey> _keySelector;

        public AddKeyEnumerator([NotNull] IChangeSet<TObject> source,
                                [NotNull] Func<TObject, TKey> keySelector)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public IEnumerator<Change<TObject, TKey>> GetEnumerator()
        {
            foreach (var change in _source)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        {
                            var item = change.Item.Current;
                            var key = _keySelector(item);
                            yield return new Change<TObject, TKey>(ChangeReason.Add, key, item);

                            break;
                        }
                    case ListChangeReason.AddRange:
                        {
                            foreach (var item in change.Range)
                            {
                                var key = _keySelector(item);
                                yield return new Change<TObject, TKey>(ChangeReason.Add, key, item);
                            }

                            break;
                        }
                    case ListChangeReason.Replace:
                        {
                            //replace is a remove and add
                            var previous = change.Item.Previous.Value;
                            var previousKey = _keySelector(previous);
                            yield return new Change<TObject, TKey>(ChangeReason.Remove, previousKey, previous);

                            var current = change.Item.Current;
                            var currentKey = _keySelector(current);
                            yield return new Change<TObject, TKey>(ChangeReason.Add, currentKey, current);

                            break;
                        }
                    case ListChangeReason.Remove:
                        {
                            var item = change.Item.Current;
                            var key = _keySelector(item);
                            yield return new Change<TObject, TKey>(ChangeReason.Remove, key, item);

                            break;
                        }
                    case ListChangeReason.Clear:
                    case ListChangeReason.RemoveRange:
                        {
                            foreach (var item in change.Range)
                            {
                                var key = _keySelector(item);
                                yield return new Change<TObject, TKey>(ChangeReason.Remove, key, item);
                            }

                            break;
                        }
                    case ListChangeReason.Moved:
                        {
                            var key = _keySelector(change.Item.Current);
                            yield return new Change<TObject, TKey>(ChangeReason.Moved, key, change.Item.Current, change.Item.Previous, change.Item.CurrentIndex, change.Item.PreviousIndex);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
