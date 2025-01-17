// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace DynamicData.Binding;

/// <summary>
/// Options for the sort and bind operator.
/// </summary>
public record struct SortAndBindOptions()
{
    /// <summary>
    /// The reset threshold ie the number of changes before a reset is fired.
    /// </summary>
    public int ResetThreshold { get; init; } = BindingOptions.DefaultResetThreshold;

    /// <summary>
    /// When possible, should replace be used instead of remove and add.
    /// </summary>
    public bool UseReplaceForUpdates { get; init; } = BindingOptions.DefaultUseReplaceForUpdates;

    /// <summary>
    /// Use binary search when the result of the comparer is a pure function.
    /// </summary>
    public bool UseBinarySearch { get; init; }

    /// <summary>
    /// Set the initial capacity of the readonly observable collection.
    /// </summary>
    public int InitialCapacity { get; init; } = -1;

    /// <summary>
    /// Reset on first time load.
    ///
    /// This is opt-in only and is only required for consumers who need to maintain
    /// backwards compatibility will the former  BindingOptions.ResetOnFirstTimeLoad.
    /// </summary>
    public bool ResetOnFirstTimeLoad { get; init; }

    /// <summary>
    /// The default main thread scheduler.  If left null, it is the responsibility of the consumer
    /// to ensure binding takes place on the main thread.
    /// </summary>
    public IScheduler? Scheduler { get; init; }
}
