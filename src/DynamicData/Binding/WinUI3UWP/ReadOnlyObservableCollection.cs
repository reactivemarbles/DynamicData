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
    public class ReadOnlyObservableCollection<T> : IList<T>, IReadOnlyCollection<T>, Microsoft.UI.Xaml.Interop.INotifyCollectionChanged, Microsoft.UI.Xaml.Data.INotifyPropertyChanged
    {
        private ObservableCollection<T> _internalCollection;

        public int Count => _internalCollection.Count;

        public bool IsReadOnly => true;

        public T this[int index] { get => _internalCollection[index]; set => throw new NotImplementedException(); }

        public ReadOnlyObservableCollection(IList<T> list)
        {
            _internalCollection = new ObservableCollection<T>(list);
            _internalCollection.CollectionChanged += InternalCollection_CollectionChanged;
            _internalCollection.PropertyChanged += InternalCollection_PropertyChanged;
            CollectionChanged?.Invoke(this, new Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset, null, null, -1, -1));
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _internalCollection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _internalCollection.GetEnumerator();
        }

        public ReadOnlyObservableCollection(IEnumerable<T> collection)
        {
            _internalCollection = new ObservableCollection<T>(collection);
            _internalCollection.CollectionChanged += InternalCollection_CollectionChanged;
            _internalCollection.PropertyChanged += InternalCollection_PropertyChanged;
            CollectionChanged?.Invoke(this, new Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset, null, null, -1, -1));
        }

        public event Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventHandler CollectionChanged;

#pragma warning disable 0067 // PropertyChanged is never used, raising a warning, but it's needed to implement INotifyPropertyChanged.
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067

        public int IndexOf(T item)
        {
            return _internalCollection.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
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
            return _internalCollection.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _internalCollection.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        private void InternalCollection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        private void InternalCollection_CollectionChanged(object sender, Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventArgs e)
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