using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal struct UnifiedChange<T>
	{

		public ListChangeReason Reason { get; }
		public T Current { get; }
		public Optional<T> Previous { get; }

		public UnifiedChange(ListChangeReason reason, T current)
			: this(reason, current, Optional.None<T>())
		{
		}

		public UnifiedChange(ListChangeReason reason, T current, Optional<T> previous)
		{
			Reason = reason;
			Current = current;
			Previous = previous;
		}

		#region Equality



		#endregion

		public override string ToString()
		{
			return string.Format("Reason: {0}, Current: {1}, Previous: {2}", Reason, Current, Previous);
		}
	}
}