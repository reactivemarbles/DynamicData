using System;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Thrown when a key is expected in a cache but not found
    /// </summary>
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
    }
}
