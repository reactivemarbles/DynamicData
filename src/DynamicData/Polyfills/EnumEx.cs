// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace System;

/// <summary>
/// Provides members for the EnumEx class.
/// </summary>
internal static class EnumEx
{
    #if NET5_0_OR_GREATER

    /// <summary>
    /// Executes the IsDefined operation.
    /// </summary>
    /// <typeparam name="TEnum">The type of the TEnum value.</typeparam>
    /// <param name="value">The value value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool IsDefined<TEnum>(TEnum value)
            where TEnum : struct, Enum
        => Enum.IsDefined(value);
    #else
        /// <summary>
    /// Executes the IsDefined operation.
    /// </summary>
    /// <typeparam name="TEnum">The type of the TEnum value.</typeparam>
    /// <param name="value">The value value.</param>
    /// <returns>The result of the operation.</returns>
public static bool IsDefined<TEnum>(TEnum value)
            where TEnum : struct, Enum
        => Enum.IsDefined(typeof(TEnum), value);
    #endif
}
