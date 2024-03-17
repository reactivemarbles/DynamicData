// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace DynamicData;

/// <summary>
/// System wide options container.
/// </summary>
public static class DynamicDataOptions
{
    /// <summary>
    /// Gets or sets the default values for all binding operations.
    /// </summary>
    public static BindingOptions Binding { get; set; } = new(BindingOptions.DefaultResetThreshold);
}


