// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>Polyfill for modern <see cref="ArgumentOutOfRangeException"/> guard helpers on target frameworks that predate them.</summary>
[ExcludeFromCodeCoverage]
internal static class ArgumentOutOfRangeExceptionHelper
{
    /// <summary>Throws when <paramref name="value"/> is negative.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value >= 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName, value, null);
    }

    /// <summary>Throws when <paramref name="value"/> is negative or zero.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    public static void ThrowIfNegativeOrZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value > 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName, value, null);
    }

    /// <summary>Throws when <paramref name="value"/> is less than <paramref name="other"/>.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="other">The lower bound.</param>
    /// <param name="paramName">The parameter name.</param>
    public static void ThrowIfLessThan(int value, int other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value >= other)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName, value, null);
    }

    /// <summary>Throws when <paramref name="value"/> is less than <paramref name="other"/>.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="other">The lower bound.</param>
    /// <param name="paramName">The parameter name.</param>
    public static void ThrowIfLessThan(TimeSpan value, TimeSpan other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value >= other)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName, value, null);
    }

    /// <summary>Throws when <paramref name="value"/> is less than or equal to <paramref name="other"/>.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="other">The lower bound.</param>
    /// <param name="paramName">The parameter name.</param>
    public static void ThrowIfLessThanOrEqual(TimeSpan value, TimeSpan other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value > other)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName, value, null);
    }
}
