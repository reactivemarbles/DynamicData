using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Disposables;

namespace DynamicData.Binding
{
    /// <summary>
    /// An override of observable collection which allows the suspension of notifications
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObservableCollectionExtended<T> : ObservableCollection<T>, IObservableCollection<T>
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.ObjectModel.ObservableCollection`1"/> class.
        /// </summary>
        public ObservableCollectionExtended()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.ObjectModel.ObservableCollection`1"/> class that contains elements copied from the specified list.
        /// </summary>
        /// <param name="list">The list from which the elements are copied.</param><exception cref="T:System.ArgumentNullException">The <paramref name="list"/> parameter cannot be null.</exception>
        public ObservableCollectionExtended(List<T> list) : base(list)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.ObjectModel.ObservableCollection`1"/> class that contains elements copied from the specified collection.
        /// </summary>
        /// <param name="collection">The collection from which the elements are copied.</param><exception cref="T:System.ArgumentNullException">The <paramref name="collection"/> parameter cannot be null.</exception>
        public ObservableCollectionExtended(IEnumerable<T> collection)
            : base(collection)
        {
        }

        #endregion

        #region Implementation of IObservableCollection

        private bool _suspendNotifications;
        private bool _suspendCount;
    
        /// <summary>
        /// Suspends notifications. When disposed, a reset notification is fired
        /// </summary>
        /// <returns></returns>
        public IDisposable SuspendNotifications()
        {
            _suspendCount = true;
            _suspendNotifications = true;
            
            return Disposable.Create(() =>
            {
                _suspendCount = false;
                _suspendNotifications = false;
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            });
        }

        /// <summary>
        /// Suspends count notifications
        /// </summary>
        /// <returns></returns>
        public IDisposable SuspendCount()
        {
            var count = this.Count;
            _suspendCount = true;
            return Disposable.Create(() =>
            {

                _suspendCount = false;
             
                if (Count!=count)
                    OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            });
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suspendCount && e.PropertyName == "Count")
                return;

            base.OnPropertyChanged(e);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suspendNotifications) return;
            base.OnCollectionChanged(e);
        }




        #endregion



        /// <summary>
        /// Clears the list and Loads the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        public void Load(IEnumerable<T> items)
        {
            CheckReentrancy();
            Clear();
         
            foreach(var item in items)
                Add(item);

        }

        #region Debugging



        ///// <summary>
        ///// Inserts an element into the <see cref="T:System.Collections.ObjectModel.Collection`1"/> at the specified index.
        ///// </summary>
        ///// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert. The value can be null for reference types.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.-or-<paramref name="index"/> is greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.</exception>
        //protected override void InsertItem(int index, T item)
        //{
        //    Console.WriteLine("Insert {0}. {1}", index, item);
        //    base.InsertItem(index, item);
        //}


        ///// <summary>
        ///// Replaces the element at the specified index.
        ///// </summary>
        ///// <param name="index">The zero-based index of the element to replace.</param><param name="item">The new value for the element at the specified index. The value can be null for reference types.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.-or-<paramref name="index"/> is greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.</exception>
        //protected override void SetItem(int index, T item)
        //{
        //    Console.WriteLine("Set {0}. {1}", index, item);
        //    base.SetItem(index, item);
        //}


        ///// <summary>
        ///// Removes the element at the specified index of the <see cref="T:System.Collections.ObjectModel.Collection`1"/>.
        ///// </summary>
        ///// <param name="index">The zero-based index of the element to remove.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.-or-<paramref name="index"/> is equal to or greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.</exception>
        //protected override void RemoveItem(int index)
        //{
        //    var item = this[index];
        //    Console.WriteLine("Remove {0}. {1}", index, item);
        //    base.RemoveItem(index);
        //}

        #endregion
    }
}