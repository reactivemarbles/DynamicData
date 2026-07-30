// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET5_0_OR_GREATER
using System.Diagnostics;

namespace System.Runtime.CompilerServices;

/// <summary>Indicates that a parameter captures the expression passed for another parameter as a string.</summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="CallerArgumentExpressionAttribute"/> class.</summary>
    /// <param name="parameterName">The name of the parameter whose expression should be captured.</param>
    public CallerArgumentExpressionAttribute(string parameterName) =>
        ParameterName = parameterName;

    /// <summary>Gets the name of the parameter whose expression should be captured.</summary>
    public string ParameterName { get; }
}
#endif
