// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines values for the DynamicallyAccessedMemberTypes enumeration.
/// </summary>
[Flags]
internal enum DynamicallyAccessedMemberTypes
{
    /// <summary>
    /// The All value.
    /// </summary>
    All = -1,

    /// <summary>
    /// The None value.
    /// </summary>
    None = 0,

    /// <summary>
    /// The PublicParameterlessConstructor value.
    /// </summary>
    PublicParameterlessConstructor = 1,

    /// <summary>
    /// The PublicConstructors value.
    /// </summary>
    PublicConstructors = 3,

    /// <summary>
    /// The NonPublicConstructors value.
    /// </summary>
    NonPublicConstructors = 4,

    /// <summary>
    /// The PublicMethods value.
    /// </summary>
    PublicMethods = 8,

    /// <summary>
    /// The NonPublicMethods value.
    /// </summary>
    NonPublicMethods = 16,

    /// <summary>
    /// The PublicFields value.
    /// </summary>
    PublicFields = 32,

    /// <summary>
    /// The NonPublicFields value.
    /// </summary>
    NonPublicFields = 64,

    /// <summary>
    /// The PublicNestedTypes value.
    /// </summary>
    PublicNestedTypes = 128,

    /// <summary>
    /// The NonPublicNestedTypes value.
    /// </summary>
    NonPublicNestedTypes = 256,

    /// <summary>
    /// The PublicProperties value.
    /// </summary>
    PublicProperties = 512,

    /// <summary>
    /// The NonPublicProperties value.
    /// </summary>
    NonPublicProperties = 1024,

    /// <summary>
    /// The PublicEvents value.
    /// </summary>
    PublicEvents = 2048,

    /// <summary>
    /// The NonPublicEvents value.
    /// </summary>
    NonPublicEvents = 4096,

    /// <summary>
    /// The Interfaces value.
    /// </summary>
    Interfaces = 8192
}

/// <summary>
/// Provides members for the DynamicallyAccessedMembersAttribute class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
internal sealed class DynamicallyAccessedMembersAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicallyAccessedMembersAttribute"/> class.
    /// </summary>
    /// <param name="memberTypes">The member types which are dynamically accessed.</param>
    public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
    {
        MemberTypes = memberTypes;
    }

    /// <summary>
    /// Gets the MemberTypes value.
    /// </summary>
    public DynamicallyAccessedMemberTypes MemberTypes { get; }
}
#endif
