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
    public class ObservableCollection<T> : Collection<T>, Microsoft.UI.Xaml.Interop.INotifyCollectionChanged, Microsoft.UI.Xaml.Data.INotifyPropertyChanged
    {
        private ReentrancyGuard? _reentrancyGuard;

        public virtual bool IsReadOnly => false;

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
        }

        public ObservableCollection(IList<T> list)
            : base(list.ToList())
        {
        }

        public ObservableCollection(IEnumerable<T> collection)
            : base(collection.ToList())
        {
        }

        public event Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Move(int oldIndex, int newIndex)
        {
            MoveItem(oldIndex, newIndex);
        }

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

        protected override void ClearItems()
        {
            CheckReentrancy();

            TestBindableVector<T> oldItems = new TestBindableVector<T>(this);

            base.ClearItems();
            OnCollectionChanged(
                NotifyCollectionChangedAction.Reset,
                null,
                oldItems,
                0,
                0);
        }

        protected override void InsertItem(int index, T item)
        {
            CheckReentrancy();

            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            base.InsertItem(index, item);
            OnCollectionChanged(
                NotifyCollectionChangedAction.Add,
                newItem,
                null,
                index,
                0);
        }

        protected virtual void MoveItem(int oldIndex, int newIndex)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[oldIndex]);
            TestBindableVector<T> newItem = new TestBindableVector<T>(oldItem);

            T item = this[oldIndex];
            RemoveAt(oldIndex);
            base.InsertItem(newIndex, item);
            OnCollectionChanged(
                NotifyCollectionChangedAction.Move,
                newItem,
                oldItem,
                newIndex,
                oldIndex);
        }

        protected override void RemoveItem(int index)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[index]);

            base.RemoveItem(index);
            OnCollectionChanged(
                NotifyCollectionChangedAction.Remove,
                null,
                oldItem,
                0,
                index);
        }

        protected override void SetItem(int index, T item)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[index]);
            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            base.SetItem(index, item);
            OnCollectionChanged(
                NotifyCollectionChangedAction.Replace,
                newItem,
                oldItem,
                index,
                index);
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