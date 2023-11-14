// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// An exception that happens when there is a problem with the key selector.
/// </summary>
[Serializable]
public class KeySelectorException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeySelectorException"/> class.
    /// </summary>
    public KeySelectorException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeySelectorException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error. </param>
    public KeySelectorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeySelectorException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception. </param><param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified. </param>
    public KeySelectorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
