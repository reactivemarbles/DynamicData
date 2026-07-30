// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET7_0_OR_GREATER
using System.Diagnostics;

namespace System.Runtime.CompilerServices;

/// <summary>Indicates that compiler support for a particular feature is required for the location where it is applied.</summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute
{
    /// <summary>The <see cref="FeatureName"/> used for the ref structs C# feature.</summary>
    public const string RefStructs = nameof(RefStructs);

    /// <summary>The <see cref="FeatureName"/> used for the required members C# feature.</summary>
    public const string RequiredMembers = nameof(RequiredMembers);

    /// <summary>Initializes a new instance of the <see cref="CompilerFeatureRequiredAttribute"/> class.</summary>
    /// <param name="featureName">The name of the required compiler feature.</param>
    public CompilerFeatureRequiredAttribute(string featureName) =>
        FeatureName = featureName;

    /// <summary>Gets the name of the required compiler feature.</summary>
    public string FeatureName { get; }
}
#endif
