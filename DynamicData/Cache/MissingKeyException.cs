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
        public MissingKeyException()
        {
        }

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
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public MissingKeyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
