// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// A simple adaptor to inject side effects into a change set observable.
/// </summary>
/// <typeparam name="T">The type of the object.</typeparam>
public interface IChangeSetAdaptor<T>
    where T : notnull
{
    /// <summary>
    /// Adapts the specified change.
    /// </summary>
    /// <param name="changes">The change.</param>
    void Adapt(IChangeSet<T> changes);
}
