using System;
using System.Collections;
using System.Collections.Generic;
using DynamicData.Annotations;

namespace DynamicData.Cache.Internal
{
    internal class RemoveKeyEnumerator<TObject, TKey> : IEnumerable<Change<TObject>>
    {
        private readonly IChangeSet<TObject, TKey> _source;

        public RemoveKeyEnumerator([NotNull] IChangeSet<TObject, TKey> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public IEnumerator<Change<TObject>> GetEnumerator()
        {
            foreach (var change in _source)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        yield return new Change<TObject>(ListChangeReason.Add, change.Current);
                        break;
                    case ChangeReason.Update:
                        yield return new Change<TObject>(ListChangeReason.Remove, change.Previous.Value);
                        yield return new Change<TObject>(ListChangeReason.Add, change.Current);
                        break;
                    case ChangeReason.Remove:
                        yield return new Change<TObject>(ListChangeReason.Remove, change.Current);
                        break;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
