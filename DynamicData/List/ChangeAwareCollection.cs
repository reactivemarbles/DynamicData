using System.Collections.ObjectModel;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData
{
	internal class ChangeAwareCollection<T> : Collection<T>
	{
		ChangeSet<T> _changes = new ChangeSet<T>();

		public IChangeSet<T> CaptureChanges()
		{
			var copy = _changes;
			_changes = new ChangeSet<T>();
			return copy;
		}

		protected override void ClearItems()
		{
			//add in reverse order as this will be more efficient for any consumers to reflect
			var changes = this.Select((t, index) => new Change<T>(ChangeReason.Remove, t, index)).Reverse();
			changes.ForEach(_changes.Add);
			base.ClearItems();
		}

		protected override void InsertItem(int index, T item)
		{
			_changes.Add(new Change<T>(ChangeReason.Add, item, index));
			base.InsertItem(index, item);
		}

		protected override void RemoveItem(int index)
		{
			var item = this[index];
			_changes.Add(new Change<T>(ChangeReason.Remove, item, index));
			base.RemoveItem(index);
		}

		protected override void SetItem(int index, T item)
		{
			var previous = this[index];
			_changes.Add(new Change<T>(ChangeReason.Update, item, previous, index, index));
			base.SetItem(index, item);
		}

		public void Move(int original, int destination)
		{

		}




	}
}
