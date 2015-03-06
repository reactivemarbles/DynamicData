using System;

namespace DynamicData
{
	public enum SortOptions
	{
		None,
		UseBinarySearch
	}

	public class SortException : Exception
	{
		public SortException()
		{
		}

		public SortException(string message) : base(message)
		{
		}

		public SortException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}



}
