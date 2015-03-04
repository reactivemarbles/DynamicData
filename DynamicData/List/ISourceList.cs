using System;
using System.Collections.Generic;

namespace DynamicData
{
	internal interface ISourceList<T> : IObservableList<T>
	{
		void Edit(Action<IList<T>> updateAction);
	}
}