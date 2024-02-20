// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// System wide default values for binding operators.
/// </summary>
/// <param name="ResetThreshold">The reset threshold ie the number of changes before a reset is fired.</param>
/// <param name="UseReplaceForUpdates"> When possible, should replace be used instead of remove and add.</param>
/// <param name="ResetOnFirstTimeLoad"> Should a reset be fired for a first time load.This option is due to historic reasons where a reset would be fired for the first time load regardless of the number of changes.</param>
public record struct BindingOptions(int ResetThreshold, bool UseReplaceForUpdates = BindingOptions.DefaultUseReplaceForUpdates, bool ResetOnFirstTimeLoad = BindingOptions.DefaultResetOnFirstTimeLoad)
{
    /// <summary>
    /// The system wide factory settings default ResetThreshold.
    /// </summary>
    public const int DefaultResetThreshold = 25;

    /// <summary>
    /// The system wide factory settings default UseReplaceForUpdates value.
    /// </summary>
    public const bool DefaultUseReplaceForUpdates = true;

    /// <summary>
    /// The system wide factory settings default ResetOnFirstTimeLoad value.
    /// </summary>
    public const bool DefaultResetOnFirstTimeLoad = true;

    /// <summary>
    /// Creates binding options to never fire a reset event.
    /// </summary>
    /// <param name="useReplaceForUpdates"> When possible, should replace be used instead of remove and add.</param>
    /// <returns> The binding options.</returns>
    public static BindingOptions NeverFireReset(bool useReplaceForUpdates = DefaultResetOnFirstTimeLoad) => new(int.MaxValue, useReplaceForUpdates, false);
}
