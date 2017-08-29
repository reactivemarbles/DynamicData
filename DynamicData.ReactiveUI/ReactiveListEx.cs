using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using ReactiveUI;

namespace DynamicData.ReactiveUI
{
    /// <summary>
    ///Reactive List extensions
    /// </summary>
    public static class ReactiveListEx
    {
		/// <summary>
		/// Converts the Reactive List into an observable change set
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this  ReactiveList<T> source)
        {
            return source.ToObservableChangeSet<ReactiveList<T>, T>();
		}

        /// <summary>
        /// Clones the ReactiveList from all changes
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this  ReactiveList<TObject> source, Func<TObject, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return source.ToObservableChangeSet<ReactiveList<TObject>, TObject>().AddKey(keySelector);
        }


        internal static void CloneReactiveList<T>(this ReactiveList<T> source, IChangeSet<T> changes)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            changes.ForEach(item =>
            {

                switch (item.Reason)
                {
                    case ListChangeReason.Add:
                        {
                            var change = item.Item;
                            var hasIndex = change.CurrentIndex >= 0;
                            if (hasIndex)
                            {
                                source.Insert(change.CurrentIndex, change.Current);
                            }
                            else
                            {
                                source.Add(change.Current);
                            }
                            break;
                        }

                    case ListChangeReason.AddRange:
                        {
                            var startingIndex = item.Range.Index;

                            if (RxApp.SupportsRangeNotifications)
                            {
                                if (startingIndex >= 0)
                                {
                                    source.InsertRange(startingIndex,item.Range);
                                }
                                else
                                {
                                    source.AddRange(item.Range);
                                }
                            }
                            else
                            {
                                if (startingIndex >= 0)
                                {
                                    item.Range.Reverse().ForEach(t => source.Insert(startingIndex, t));
                                }
                                else
                                {
                                    item.Range.ForEach(source.Add);
                                }
                            }

                            break;
                        }

                    case ListChangeReason.Clear:
                        {
                            source.Clear();
                            break;
                        }

                    case ListChangeReason.Replace:
                        {

                            var change = item.Item;
                            bool hasIndex = change.CurrentIndex >= 0;
                            if (hasIndex && change.CurrentIndex == change.PreviousIndex)
                            {
                                source[change.CurrentIndex] = change.Current;
                            }
                            else
                            {
                                source.RemoveAt(change.PreviousIndex);
                                source.Insert(change.CurrentIndex, change.Current);
                            }
                        }
                        break;
                    case ListChangeReason.Remove:
                        {
                            var change = item.Item;
                            bool hasIndex = change.CurrentIndex >= 0;
                            if (hasIndex)
                            {
                                source.RemoveAt(change.CurrentIndex);
                            }
                            else
                            {
                                source.Remove(change.Current);
                            }
                            break;
                        }

                    case ListChangeReason.RemoveRange:
                        {
                            if (RxApp.SupportsRangeNotifications && item.Range.Index>=0)
                            {
                                source.RemoveRange(item.Range.Index, item.Range.Count);
                            }
                            else
                            {
                                source.RemoveMany(item.Range);
                            }
                        }
                        break;

                    case ListChangeReason.Moved:
                        {
                            var change = item.Item;
                            bool hasIndex = change.CurrentIndex >= 0;
                            if (!hasIndex)
                                throw new UnspecifiedIndexException("Cannot move as an index was not specified");

                            source.Move(change.PreviousIndex, change.CurrentIndex);
                            break;
                        }
                }
            });


        }

        internal static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
                action(item);
       
        }

        internal static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            var i = -1;
            foreach (var item in source)
                action(item,i++);
          
        }
    }
}