// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// System wide default values for binding operators.
/// </summary>
/// <param name="UseReplaceForUpdates">The reset threshold ie the number of changes before a reset is fired.</param>
/// <param name="ResetOnFirstTimeLoad"> Should a reset be fired for a first time load.This option is due to historic reasons where a reset would be fired for the first time load regardless of the number of changes.</param>
/// <param name="UseReplaceForUpdates"> When possible, should replace be used instead of remove and add.</param>
public record struct BindingOptions(int ResetThreshold, bool UseReplaceForUpdates = true, bool ResetOnFirstTimeLoad = true)
{
    /// <summary>
    /// Creates binding options to never fire a reset event.
    /// </summary>
    /// <param name="useReplaceForUpdates"> When possible, should replace be used instead of remove and add.</param>
    /// <returns> The binding options.</returns>
    public static BindingOptions NeverFireReset(bool useReplaceForUpdates = true)
    {
        return new BindingOptions(int.MaxValue, useReplaceForUpdates, false);
    }
}

/// <summary>
/// System wide default values for binding operators.
/// </summary>
public static class DynamicDataOptions
{
    /// <summary>
    /// Gets or sets the default values for all binding operations.
    /// </summary>
    public static BindingOptions Binding { get; set; } = new(25);
}
