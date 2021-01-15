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
    public class ReadOnlyObservableCollection<T> : ReadOnlyCollection<T>, Microsoft.UI.Xaml.Interop.INotifyCollectionChanged, Microsoft.UI.Xaml.Data.INotifyPropertyChanged
    {
        public ReadOnlyObservableCollection(IList<T> list)
            : base(list.ToList())
        {
            if (list is Microsoft.UI.Xaml.Interop.INotifyCollectionChanged)
            {
                ((Microsoft.UI.Xaml.Interop.INotifyCollectionChanged)list).CollectionChanged += ReadOnlyObservableCollection_CollectionChanged;
            }
        }

        private void ReadOnlyObservableCollection_CollectionChanged(object sender, Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        public ReadOnlyObservableCollection(IEnumerable<T> collection)
            : base(collection.ToList())
        {
        }

        public event Microsoft.UI.Xaml.Interop.NotifyCollectionChangedEventHandler CollectionChanged;

#pragma warning disable 0067 // PropertyChanged is never used, raising a warning, but it's needed to implement INotifyPropertyChanged.
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore SA1201
#pragma warning restore SA1402

#endif