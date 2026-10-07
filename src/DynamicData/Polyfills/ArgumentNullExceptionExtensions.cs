// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#pragma warning disable SA1313

#if !NET7_0_OR_GREATER

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

[ExcludeFromCodeCoverage]
internal static class ArgumentNullExceptionExtensions
{
    extension(ArgumentNullException _)
    {
        public static void ThrowIfNull(
            [NotNull] object? argument,
            [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is not null)
                return;

            throw new ArgumentNullException(paramName);
        }
    }
}

#endif
