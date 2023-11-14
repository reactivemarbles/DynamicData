// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// Represents an observable collection where collection changed and count notifications can be suspended.
/// </summary>
public interface INotifyCollectionChangedSuspender
{
    /// <summary>
    /// Suspends count notifications.
    /// </summary>
    /// <returns>A disposable which when disposed re-activates count notifications.</returns>
    IDisposable SuspendCount();

    /// <summary>
    /// Suspends notifications. When disposed, a reset notification is fired.
    /// </summary>
    /// <returns>A disposable which when disposed re-activates notifications.</returns>
    IDisposable SuspendNotifications();
}
