// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace DynamicData.Binding
{
    /// <summary>
    /// Represents an observable collection where collection changed and count notifications can be suspended 
    /// </summary>
    public interface INotifyCollectionChangedSuspender
    {
        /// <summary>
        /// Suspends notifications. When disposed, a reset notification is fired
        /// </summary>
        IDisposable SuspendNotifications();

        /// <summary>
        /// Suspends count notifications
        /// </summary>
        IDisposable SuspendCount();
    }
}