// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Thrown when a key is expected in a cache but not found.
/// </summary>
[Serializable]
public class MissingKeyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingKeyException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MissingKeyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingKeyException"/> class.
    /// </summary>
    public MissingKeyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingKeyException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">A inner exception with further information.</param>
    public MissingKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
