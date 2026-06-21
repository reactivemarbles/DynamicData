// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Provides members for the Constants class.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The VirtualizeIsObsolete field.
    /// </summary>
    public const string VirtualizeIsObsolete = "Use SortAndVirtualize as it's more efficient";

    /// <summary>
    /// The PageIsObsolete field.
    /// </summary>
    public const string PageIsObsolete = "Use SortAndPage as it's more efficient";

    /// <summary>
    /// The TopIsObsolete field.
    /// </summary>
    public const string TopIsObsolete = "Use Overload with comparer as it's more efficient";

    /// <summary>
    /// The SortIsObsolete field.
    /// </summary>
    public const string SortIsObsolete = "Use SortAndBind as it's more efficient";
}
