using System;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Thrown when an exception occurs within the sort operators
    /// </summary>
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
    }
}
