using System.Collections.Generic;
using System.Linq;

namespace DynamicData.List.Linq
{
    internal class Reverser<T>
    {
        private int _length = 0;

        public IEnumerable<Change<T>> Reverse(IChangeSet<T> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        {
                            yield return new Change<T>(ListChangeReason.Add, change.Item.Current, _length - change.Item.CurrentIndex);
                            _length++;
                            break;
                        }
                    case ListChangeReason.AddRange:
                        {
                            var offset = change.Range.Index == -1 ? 0 : _length - change.Range.Index;
                            yield return new Change<T>(ListChangeReason.AddRange, change.Range.Reverse(), offset);
                            _length += change.Range.Count;

                            break;
                        }
                    case ListChangeReason.Replace:
                        {
                            var newIndex = _length - change.Item.CurrentIndex - 1;
                            yield return new Change<T>(ListChangeReason.Replace, change.Item.Current, change.Item.Previous.Value, newIndex, newIndex);
                            break;
                        }
                    case ListChangeReason.Remove:
                        {
                            yield return new Change<T>(ListChangeReason.Remove, change.Item.Current, _length - change.Item.CurrentIndex - 1);
                            _length--;
                            break;
                        }
                    case ListChangeReason.RemoveRange:
                        {
                            var offset = _length - change.Range.Index - change.Range.Count;
                            yield return new Change<T>(ListChangeReason.RemoveRange, change.Range.Reverse(), offset);
                            _length -= change.Range.Count;

                            break;
                        }
                    case ListChangeReason.Moved:
                        {
                            var currentIndex = _length - change.Item.CurrentIndex - 1;
                            var previousIndex = _length - change.Item.PreviousIndex - 1;
                            yield return new Change<T>(change.Item.Current, currentIndex, previousIndex);

                            break;
                        }
                    case ListChangeReason.Clear:
                        {
                            yield return new Change<T>(ListChangeReason.Clear, change.Range.Reverse());
                            _length = 0;
                            break;
                        }
                }
            }
        }
    }
}
