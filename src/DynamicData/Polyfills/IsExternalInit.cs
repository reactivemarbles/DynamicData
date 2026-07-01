// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET5_0_OR_GREATER
using System.Diagnostics;

namespace System.Runtime.CompilerServices;

/// <summary>Reserved for the compiler to track metadata for <see langword="init"/>-only members; not for use in source.</summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[SuppressMessage(
    "Design",
    "SST1436:Add members to this type or remove it; an empty type is rarely intentional",
    Justification = "Compiler-recognized marker type for init-only members; it must remain empty.")]
internal static class IsExternalInit;
#endif
