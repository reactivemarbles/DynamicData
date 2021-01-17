// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if WINUI3UWP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    public class ReadOnlyObservableCollection<T> : ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>, ICollection, IList, Microsoft.UI.Xaml.Interop.INotifyCollectionChanged, Microsoft.UI.Xaml.Data.INotifyPropertyChanged
    {
        private ObservableCollection<T> _wrappedCollection;
        private object _syncRoot = new object();

        public ReadOnlyObservableCollection(ObservableCollection<T> list)
        {
            _wrappedCollection = list;
            _wrappedCollection.PropertyChanged += WrappedCollection_PropertyChanged;
            _wrappedCollection.CollectionChanged += WrappedCollection_CollectionChanged;
        }

        public void Add(T item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            return _wrappedCollection.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _wrappedCollection.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _wrappedCollection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _wrappedCollection.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _wrappedCollection.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            (_wrappedCollection as ICollection).CopyTo(array, index);
        }

        public int Add(object value)
        {
            throw new NotImplementedException();
        }

        public bool Contains(object value)
        {
            return _wrappedCollection.Contains((T)value);
        }

        public int IndexOf(object value)
        {
            return _wrappedCollection.IndexOf((T)value);
        }

        public void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly => false;

        public int Count => _wrappedCollection.Count;

        public bool IsSynchronized => true;

        public object SyncRoot => _syncRoot;

        public bool IsFixedSize => false;

        object IList.this[int index] { get => _wrappedCollection[index]; set => throw new NotImplementedException(); }

        public T this[int index] { get => _wrappedCollection[index]; set => throw new NotImplementedException(); }

        public event Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        private void WrappedCollection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        private void WrappedCollection_CollectionChanged(object sender, Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore SA1201
#pragma warning restore SA1402

#endif