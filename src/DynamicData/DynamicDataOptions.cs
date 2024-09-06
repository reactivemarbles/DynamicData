// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using DynamicData.Binding;

namespace DynamicData;

/// <summary>
/// System-wide options container.
/// </summary>
public static class DynamicDataOptions
{
    /// <summary>
    /// Gets or sets the system-wide default values for all Bind operations.
    /// </summary>
    public static BindingOptions Binding { get; set; } = new(BindingOptions.DefaultResetThreshold);

    /// <summary>
    /// Gets or sets the system-wide default values for all SortAndBind operations.
    /// </summary>
    public static SortAndBindOptions SortAndBind { get; set; } = new();

    /// <summary>
    /// The default main thread scheduler.  If left null, it is the responsibility of the consumer
    /// to ensure binding takes place on the main thread.
    /// </summary>
    public static IScheduler? BindingScheduler { get; set; }
}
