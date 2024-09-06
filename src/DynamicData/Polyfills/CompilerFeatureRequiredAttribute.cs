// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if !NET7_0_OR_GREATER
namespace System.Runtime.CompilerServices;

// Allows use of the C#11 `required` keyword, internally within this library, when targeting frameworks older than .NET 7.
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute(string featureName)
        : Attribute
{
    public const string RefStructs
        = nameof(RefStructs);

    public const string RequiredMembers
        = nameof(RequiredMembers);

    public string FeatureName { get; } = featureName;

    public bool IsOptional { get; init; }
}
#endif
