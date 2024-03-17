// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace DynamicData.Binding;

/// <summary>
/// Options for bind the bind and sort operators.
/// </summary>
/// <param name="ResetThreshold">The reset threshold ie the number of changes before a reset is fired.</param>
/// <param name="UseReplaceForUpdates"> When possible, should replace be used instead of remove and add.</param>
/// <param name="ResetOnFirstTimeLoad"> Should a reset be fired for a first time load.This option is due to historic reasons where a reset would be fired for the first time load regardless of the number of changes.</param>
/// <param name="UseBinarySearch"> Use binary search when the result of the comparer is a pure function.</param>
/// <param name="InitialCapacity"> Set the initial capacity of the readonly observable collection.</param>
public record struct BindAndSortOptions(
    int ResetThreshold,
    bool UseReplaceForUpdates,
    bool ResetOnFirstTimeLoad,
    bool UseBinarySearch,
    int InitialCapacity)
{
    /// <summary>
    /// Default bind and sort options.
    /// </summary>
    public static readonly BindAndSortOptions Default = new
    (
        BindingOptions.DefaultResetThreshold,
        BindingOptions.DefaultUseReplaceForUpdates,
        BindingOptions.DefaultResetOnFirstTimeLoad,
        false,
        -1);
}
