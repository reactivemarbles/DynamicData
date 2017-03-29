using System.Collections;
using System.Collections.Generic;
using DynamicData.List.Internal;

namespace DynamicData.List.Linq
{
    internal class UnifiedChangeEnumerator<T> : IEnumerable<UnifiedChange<T>>
    {
        private readonly IChangeSet<T> _changeSet;

        public UnifiedChangeEnumerator(IChangeSet<T> changeSet)
        {
            _changeSet = changeSet;
        }

        public IEnumerator<UnifiedChange<T>> GetEnumerator()
        {
            foreach (var change in _changeSet)
            {
                if (change.Type == ChangeType.Item)
                {
                    yield return new UnifiedChange<T>(change.Reason, change.Item.Current, change.Item.Previous);
                }
                else
                {
                    foreach (var item in change.Range)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.AddRange:
                                yield return new UnifiedChange<T>(ListChangeReason.Add, item);
                                break;
                            case ListChangeReason.RemoveRange:
                                yield return new UnifiedChange<T>(ListChangeReason.Remove, item);
                                break;
                            case ListChangeReason.Clear:
                                yield return new UnifiedChange<T>(ListChangeReason.Clear, item);
                                break;
                            default:
                                yield break;
                        }
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
