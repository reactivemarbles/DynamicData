// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData
{
    internal static class ExceptionMixins
    {
        public static void ThrowArgumentNullExceptionIfNull<T>(this T? value, string name)
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
