// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Represents a subset of data reduced by a defined set of parameters
/// Changes are always published in the order.
/// </summary>
/// <typeparam name="T">The type of the object.</typeparam>
public interface IVirtualChangeSet<T> : IChangeSet<T>
    where T : notnull
{
    /// <summary>
    /// Gets the parameters used to virtualise the stream.
    /// </summary>
    IVirtualResponse Response { get; }
}
