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
    public class ReadOnlyObservableCollection<T> : ObservableCollection<T>
    {
        public override bool IsReadOnly => true;
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore SA1201
#pragma warning restore SA1402

#endif