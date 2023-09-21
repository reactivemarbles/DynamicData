// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel;

/// <summary>
/// Connectable cache status.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Status set to pending until first batch of data is received.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Status set to loaded when first batch of data has been received.  Remains loaded
    /// until the cache is disposed or faults.
    /// </summary>
    Loaded = 1,

    /// <summary>
    /// There has been a error and the stream has stopped. No more status updates will be received.
    /// </summary>
    Errored = 2,

    /// <summary>
    /// The stream has completed. No more status updates will be received.
    /// </summary>
    Completed = 3
}
