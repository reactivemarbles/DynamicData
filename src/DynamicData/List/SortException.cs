// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Thrown when an exception occurs within the sort operators
    /// </summary>
    [Serializable]
    public class SortException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SortException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SortException(string message)
            : base(message)
        {
        }

        public SortException()
        {
        }

        public SortException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SortException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }
    }
}
