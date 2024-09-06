// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if !NETCOREAPP
namespace System.Runtime.CompilerServices;

// Allows use of the C#11 `init` keyword, internally within this library, when targeting frameworks older than .NET 5.
internal sealed class IsExternalInit;
#endif
