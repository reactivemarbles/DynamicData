// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace System;

internal static class EnumEx
{
    #if NET5_0_OR_GREATER
    public static bool IsDefined<TEnum>(TEnum value)
            where TEnum : struct, Enum
        => Enum.IsDefined(value);
    #else
    public static bool IsDefined<TEnum>(TEnum value)
            where TEnum : struct, Enum
        => Enum.IsDefined(typeof(TEnum), value);
    #endif
}
