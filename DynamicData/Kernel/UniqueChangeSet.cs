using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{



    internal class UniqueChangeSet<TObject, TKey> : IChangeSet<TObject, TKey>
    {
        #region Fields

  
        private readonly HashSet<Change<TObject, TKey>> _items = new HashSet<Change<TObject, TKey>>(KeyComparerInstance);
        private static readonly KeyOnlyComparer KeyComparerInstance = new KeyOnlyComparer();

        private int _adds;
        private int _removes;
        private int _evaluates;
        private int _updates;
        private int _moves;

        #endregion

        private class KeyOnlyComparer : IEqualityComparer<Change<TObject, TKey>>
        {
            public bool Equals(Change<TObject, TKey> x, Change<TObject, TKey> y)
            {
                return x.Key.Equals(y.Key);
            }

            public int GetHashCode(Change<TObject, TKey> obj)
            {
                return obj.Key.GetHashCode();
            }
        }

        #region Construction

        public UniqueChangeSet(IEnumerable<Change<TObject, TKey>> items)
        {
            //foreach (var update in items)
            //{
            //    Add(update);
            //}

            var batched = items.GroupBy(t => t.Key).ToList();

            batched.ForEach(grouped =>
                            {
                                //there has only been a single update
                                //so add it,
                                if (grouped.Count() == 1)
                                {
                                    Add(grouped.First());
                                }
                                else
                                {

                                    var change = CalculateChange(grouped);
                                   if (change.HasValue)
                                        Add(change.Value);
                                }
                            });
        }


        /// <summary>
        /// Determines what the change should be an item with the same key has changed multiple times in a batch update
        /// </summary>
        /// <param name="changesPerKey">The changes per key.</param>
        /// <returns></returns>
        private Optional<Change<TObject,TKey>> CalculateChange(IEnumerable<Change<TObject,TKey>> changesPerKey)
        {
            var perKey = changesPerKey as IList<Change<TObject, TKey>> ?? changesPerKey.ToList();
           // var first = perKey.First();

            Change<TObject, TKey> cursor = perKey.First();
            foreach (var next in perKey.Skip(1))
            {
                    switch (next.Reason)
                    {
                        case ChangeReason.Add:
                            cursor = next;
                            break;
                       
                        case ChangeReason.Update:
                            {
                                switch (cursor.Reason)
                                {
                                    case ChangeReason.Add:
                                        cursor = next;
                                        break;
                                    case ChangeReason.Update:
                                        cursor = new Change<TObject, TKey>(ChangeReason.Add, next.Key, next.Current, next.Previous);
                             
                                        break;
                                    case ChangeReason.Remove:
                                        cursor = new Change<TObject, TKey>(ChangeReason.Add,next.Key, next.Current,next.Previous);
                                        break;
                                    case ChangeReason.Evaluate:
                                        cursor = next;
                                        break;
                                    case ChangeReason.Moved:
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                                break;
                        case ChangeReason.Remove:

                            cursor = next;
                            break;
                        case ChangeReason.Evaluate:
                            break;
                        case ChangeReason.Moved:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

            }
            return perKey.Last();

        }


        public UniqueChangeSet()
        {
        }


        public void Add(Change<TObject, TKey> item)
        {
            switch (item.Reason)
            {
                case ChangeReason.Add:
                    if (_items.Contains(item))
                    {
                        _items.Remove(item);
                        _adds--;
                    }
                    _items.Add(item);
                    _adds++;
                    break;
                case ChangeReason.Update:
                    if (_items.Contains(item))
                    {
                        _items.Remove(item);
                        _updates--;
                    }
                    _items.Add(item);
                    _updates++;
                    break;
                case ChangeReason.Remove:
                    if (_items.Contains(item))
                    {
                        _items.Remove(item);
                        _removes--;
                    }
                    _items.Add(item);
                    _removes++;
                    break;
                case ChangeReason.Evaluate:

                    //an evaluate can never be of a higher precedent than an imperative update
                    //so only include requery if not already added
                    if (!_items.Contains(item))
                    {
                        _items.Add(item);
                        _evaluates++;
                    }
                    break;
                default:
                    break;
            }
        }



        #endregion

        #region Properties
        

        public int Count
        {
            get { return _items.Count; }
        }

        public int Adds
        {
            get { return _adds; }
        }

        public int Updates
        {
            get { return _updates; }
        }

        public int Removes
        {
            get { return _removes; }
        }

        public int Evaluates
        {
            get { return _evaluates; }
        }


        public int Moves
        {
            get { return _moves; }
        }

        #endregion

        #region Enumeration

        public IEnumerator<Change<TObject, TKey>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        #endregion



        public override string ToString()
        {
            return string.Format("ChangeSet<{0}.{1}>. Count={2}", typeof (TObject).Name, typeof (TKey).Name,
                                 Count);
        }
    }
}