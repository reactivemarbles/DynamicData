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
    public class ObservableCollectionExtended<T> : ObservableCollection<T>, IObservableCollection<T>, IExtendedList<T>
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
        public ObservableCollectionExtended(List<T> list)
            : base(list)
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
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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

                if (Count != count)
                    OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            });
        }

        /// <summary>
        /// Raises the <see cref="E:PropertyChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suspendCount && e.PropertyName == "Count")
                return;

            base.OnPropertyChanged(e);
        }

        /// <summary>
        /// Raises the <see cref="E:CollectionChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
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

            foreach (var item in items)
                Add(item);
        }

        #region Implementation of IExtendedList
        public void AddRange(IEnumerable<T> collection)
        {
            foreach (var item in collection)
                Add(item);
        }

        public void InsertRange(IEnumerable<T> collection, int index)
        {
            foreach (var item in collection)
                InsertItem(index++, item);
        }

        public void RemoveRange(int index, int count)
        {
            for (var i = 0; i < count; i++)
                RemoveAt(index);
        }
        #endregion
    }
}
