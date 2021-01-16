// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if WINUI3UWP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Interop;

using NotifyCollectionChangedAction = Microsoft.UI.Xaml.Interop.NotifyCollectionChangedAction;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1201
#pragma warning disable SA1402

namespace DynamicData.Binding.WinUI3UWP
{
    /// <summary>
    /// Replacement ObservableCollection for use only in WinUI3-UWP apps.
    /// </summary>
    /// <typeparam name="T">Anything.</typeparam>
    public class ObservableCollection<T> : IList<T>, Microsoft.UI.Xaml.Interop.INotifyCollectionChanged, Microsoft.UI.Xaml.Data.INotifyPropertyChanged
    {
        private List<T> _backingCollection;
        private ReentrancyGuard? _reentrancyGuard;

        // private IDisposable _notifyCollectionSuspended;
        // private IDisposable _notifyCountSuspended;
        public int Count => _backingCollection.Count;

        public virtual bool IsReadOnly => false;

        public T this[int index]
        {
            get => _backingCollection[index];
            set
            {
                if (!_backingCollection[index].Equals(value))
                {
                    TestBindableVector<T> oldItem = new TestBindableVector<T>();
                    oldItem.Add(this[index]);
                    TestBindableVector<T> newItem = new TestBindableVector<T>();
                    newItem.Add(value);

                    _backingCollection[index] = value;
                    OnCollectionChanged(
                        NotifyCollectionChangedAction.Replace,
                        newItem,
                        oldItem,
                        index,
                        index);
                }
            }
        }

        private class ReentrancyGuard : IDisposable
        {
            private ObservableCollection<T> _owningCollection;

            public ReentrancyGuard(ObservableCollection<T> owningCollection)
            {
                owningCollection.CheckReentrancy();
                owningCollection._reentrancyGuard = this;
                _owningCollection = owningCollection;
            }

            public void Dispose()
            {
                _owningCollection._reentrancyGuard = null;
            }
        }

        public ObservableCollection()
        {
            _backingCollection = new List<T>();
        }

        public ObservableCollection(IList<T> list)
        {
            _backingCollection = new List<T>(list);
        }

        public ObservableCollection(IEnumerable<T> collection)
        {
            _backingCollection = new List<T>(collection);
        }

        public event Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventHandler CollectionChanged;

        public void Move(int oldIndex, int newIndex)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[oldIndex]);
            TestBindableVector<T> newItem = new TestBindableVector<T>(oldItem);

            T item = this[oldIndex];
            _backingCollection.RemoveAt(oldIndex);
            _backingCollection.Insert(newIndex, item);
            OnCollectionChanged(
                NotifyCollectionChangedAction.Move,
                newItem,
                oldItem,
                newIndex,
                oldIndex);
        }

        public int IndexOf(T item)
        {
            return _backingCollection.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            CheckReentrancy();

            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            _backingCollection.Insert(index, item);
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(
                NotifyCollectionChangedAction.Add,
                newItem,
                null,
                index,
                0);
        }

        public void RemoveAt(int index)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[index]);

            _backingCollection.RemoveAt(index);
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(
                NotifyCollectionChangedAction.Remove,
                null,
                oldItem,
                0,
                index);
        }

        public void Add(T item)
        {
            CheckReentrancy();

            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            _backingCollection.Add(item);
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(
                NotifyCollectionChangedAction.Add,
                newItem,
                null,
                _backingCollection.Count - 1,
                0);
        }

        public void Clear()
        {
            CheckReentrancy();

            TestBindableVector<T> oldItems = new TestBindableVector<T>(this);

            _backingCollection.Clear();
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(
                NotifyCollectionChangedAction.Reset,
                null,
                oldItems,
                0,
                0);
        }

        public bool Contains(T item)
        {
            return _backingCollection.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _backingCollection.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(item);
            var oldIndex = _backingCollection.IndexOf(item);

            var result = _backingCollection.Remove(item);
            if (result)
            {
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnCollectionChanged(
                    NotifyCollectionChangedAction.Remove,
                    null,
                    oldItem,
                    0,
                    oldIndex);
            }

            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _backingCollection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _backingCollection.GetEnumerator();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected IDisposable BlockReentrancy()
        {
            return new ReentrancyGuard(this);
        }

        protected void CheckReentrancy()
        {
            if (_reentrancyGuard != null)
            {
                throw new InvalidOperationException("Collection cannot be modified in a collection changed handler.");
            }
        }

        protected void ClearItems()
        {
            CheckReentrancy();

            TestBindableVector<T> oldItems = new TestBindableVector<T>(this);

            _backingCollection.Clear();
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(
                NotifyCollectionChangedAction.Reset,
                null,
                oldItems,
                0,
                0);
        }

        protected virtual void OnCollectionChanged(
            NotifyCollectionChangedAction action,
            IBindableVector newItems,
            IBindableVector oldItems,
            int newIndex,
            int oldIndex)
        {
            OnCollectionChanged(new Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventArgs(action, newItems, oldItems, newIndex, oldIndex));
        }

        protected virtual void OnCollectionChanged(Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventArgs e)
        {
            using (BlockReentrancy())
            {
                // if (e.Action != NotifyCollectionChangedAction.Replace)
                // {
                //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
                // }
                CollectionChanged?.Invoke(this, e);
            }
        }

        // protected virtual void OnPropertyChanged(Microsoft.UI.Xaml.Data.PropertyChangedEventArgs e)
        protected virtual void OnPropertyChanged(Microsoft.UI.Xaml.Data.PropertyChangedEventArgs e)
        {
            using (BlockReentrancy())
            {
                PropertyChanged?.Invoke(this, e);
            }
        }

        protected void InsertItem(int index, T item)
        {
            CheckReentrancy();

            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            _backingCollection.Insert(index, item);
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(
                NotifyCollectionChangedAction.Add,
                newItem,
                null,
                index,
                0);
        }

        protected void MoveItem(int oldIndex, int newIndex)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[oldIndex]);
            TestBindableVector<T> newItem = new TestBindableVector<T>(oldItem);

            T item = this[oldIndex];
            _backingCollection.RemoveAt(oldIndex);
            _backingCollection.Insert(newIndex, item);
            OnCollectionChanged(
                NotifyCollectionChangedAction.Move,
                newItem,
                oldItem,
                newIndex,
                oldIndex);
        }

        protected void RemoveItem(int index)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[index]);

            _backingCollection.RemoveAt(index);
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(
                NotifyCollectionChangedAction.Remove,
                null,
                oldItem,
                0,
                index);
        }

        protected void SetItem(int index, T item)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[index]);
            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            _backingCollection[index] = item;
            OnCollectionChanged(
                NotifyCollectionChangedAction.Replace,
                newItem,
                oldItem,
                index,
                index);
        }
    }

    public class TestBindableVector<T> : IList<T>, IBindableVector
    {
        private IList<T> _implementation;

        public TestBindableVector()
        {
            _implementation = new List<T>();
        }

        public TestBindableVector(IList<T> list)
        {
            _implementation = new List<T>(list);
        }

        public T this[int index] { get => _implementation[index]; set => _implementation[index] = value; }

        public int Count => _implementation.Count;

        public virtual bool IsReadOnly => _implementation.IsReadOnly;

        public void Add(T item)
        {
            _implementation.Add(item);
        }

        public void Clear()
        {
            _implementation.Clear();
        }

        public bool Contains(T item)
        {
            return _implementation.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _implementation.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _implementation.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _implementation.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _implementation.Insert(index, item);
        }

        public bool Remove(T item)
        {
            return _implementation.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _implementation.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _implementation.GetEnumerator();
        }

        public object GetAt(uint index)
        {
            return _implementation[(int)index];
        }

        public IBindableVectorView GetView()
        {
            return new TestBindableVectorView<T>(_implementation);
        }

        public bool IndexOf(object value, out uint index)
        {
            int indexOf = _implementation.IndexOf((T)value);

            if (indexOf >= 0)
            {
                index = (uint)indexOf;
                return true;
            }

            index = 0;
            return false;
        }

        public void SetAt(uint index, object value)
        {
            _implementation[(int)index] = (T)value;
        }

        public void InsertAt(uint index, object value)
        {
            _implementation.Insert((int)index, (T)value);
        }

        public void RemoveAt(uint index)
        {
            _implementation.RemoveAt((int)index);
        }

        public void Append(object value)
        {
            _implementation.Add((T)value);
        }

        public void RemoveAtEnd()
        {
            _implementation.RemoveAt(_implementation.Count - 1);
        }

        public uint Size => (uint)_implementation.Count;

        public IBindableIterator First()
        {
            return new TestBindableIterator<T>(_implementation);
        }
    }

    public class TestBindableVectorView<T> : TestBindableVector<T>, IBindableVectorView
    {
        public TestBindableVectorView(IList<T> list)
            : base(list)
        {
        }

        public override bool IsReadOnly => true;
    }

    public class TestBindableIterator<T> : IBindableIterator
    {
        private readonly IEnumerator<T> _enumerator;

        public TestBindableIterator(IEnumerable<T> enumerable)
        {
            _enumerator = enumerable.GetEnumerator();
        }

        public bool MoveNext()
        {
            return _enumerator.MoveNext();
        }

        public object Current => _enumerator.Current;

        public bool HasCurrent => _enumerator.Current != null;
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore SA1201
#pragma warning restore SA1402

#endif