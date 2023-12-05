// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace DynamicData.Kernel;

/// <inheritdoc />
public class IndexMinusOneException : Exception
{
    private const string DefaultMessage = "Index of minus one detected. Please raise an issue for DynamicData with an exact re-production";

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexMinusOneException"/> class.
    /// </summary>
    /// <param name="context"> Some context.</param>
    /// <param name="member"> The calling member.</param>
    /// <param name="line"> The calling line number..</param>
    public IndexMinusOneException(string context, [CallerMemberName] string? member = null, [CallerLineNumber] int line = 0)
        : base($"{DefaultMessage}. Context={context}. Member={member}. Line={line}")
    {
    }
}
