// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System.Diagnostics.CodeAnalysis;

/// <summary>Specifies that when a method returns <see cref="ReturnValue"/>, the parameter is not <see langword="null"/>.</summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class NotNullWhenAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="NotNullWhenAttribute"/> class.</summary>
    /// <param name="returnValue">The return value condition for which the parameter is not <see langword="null"/>.</param>
    public NotNullWhenAttribute(bool returnValue) =>
        ReturnValue = returnValue;

    /// <summary>Gets the return value condition for which the parameter is not <see langword="null"/>.</summary>
    public bool ReturnValue { get; }
}
#endif
