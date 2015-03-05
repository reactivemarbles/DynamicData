using System;
using System.Collections.Generic;

namespace DynamicData
{
	public interface ISourceList<T> : IObservableList<T>
	{
		/// <summary>
		/// Edit the inner list within the list's internal locking mechanism
		/// </summary>
		/// <param name="updateAction">The update action.</param>
		void Edit(Action<IList<T>> updateAction);
	}
}