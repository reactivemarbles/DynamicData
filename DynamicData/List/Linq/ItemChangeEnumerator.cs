using System.Collections;
using System.Collections.Generic;

namespace DynamicData.List.Linq
{
    internal class ItemChangeEnumerator<T> : IEnumerable<ItemChange<T>>
    {
        private readonly IChangeSet<T> _changeSet;

        public ItemChangeEnumerator(IChangeSet<T> changeSet)
        {
            _changeSet = changeSet;
        }

        public IEnumerator<ItemChange<T>> GetEnumerator()
        {
            var lastKnownIndex = 0;

            foreach (var change in _changeSet)
            {
                if (change.Type == ChangeType.Item)
                {
                    lastKnownIndex = change.Item.CurrentIndex;
                    yield return new ItemChange<T>(change.Reason, change.Item.Current, change.Item.Previous, change.Item.CurrentIndex, change.Item.PreviousIndex);
                }
                else
                {
                    int index;
                    index = change.Range.Index == -1 ? lastKnownIndex : change.Range.Index;

                    foreach (var item in change.Range)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.AddRange:
                                yield return new ItemChange<T>(ListChangeReason.Add, item, index);
                                break;
                            case ListChangeReason.RemoveRange:
                                yield return new ItemChange<T>(ListChangeReason.Remove, item, index);
                                break;
                            case ListChangeReason.Clear:
                                yield return new ItemChange<T>(ListChangeReason.Remove, item, index);
                                break;
                            default:
                                yield break;
                        }
                        index++;
                        lastKnownIndex = index;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
