// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis;

/// <summary>Specifies that the listed members are not <see langword="null"/> when the method returns the given value.</summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = true)]
internal sealed class MemberNotNullWhenAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MemberNotNullWhenAttribute"/> class.</summary>
    /// <param name="returnValue">The return value condition for which the members are not <see langword="null"/>.</param>
    /// <param name="members">The field and property members that are promised to be not-<see langword="null"/>.</param>
    public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
    {
        ReturnValue = returnValue;
        Members = members;
    }

    /// <summary>Gets the return value condition for which the members are not <see langword="null"/>.</summary>
    public bool ReturnValue { get; }

    /// <summary>Gets the field and property member names that are promised to be not-<see langword="null"/>.</summary>
    public string[] Members { get; }
}
#endif
