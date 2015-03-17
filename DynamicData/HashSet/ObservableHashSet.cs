using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace DynamicData.HashSet
{
	using System;
	using System.Collections;
	using System.Collections.Generic;

	/// <summary>
	/// Provides a set of static methods for querying objects that implement IEnumerable&lt;T&gt;.
	/// </summary>
	/// <remarks>
	/// These are extensions to System.Linq.Enuemrable and declared and implemented in the same fashion.
	/// </remarks>
	/// 
	/// 
	public static class EnumerableExtensions
	{
		// This provides a useful extension-like method to find the index of and item from IEnumerable<T>
		// This was based off of the Enumerable.Count<T> extension method.
		/// <summary>
		/// Returns the index of an item in a sequence.
		/// </summary>
		/// <typeparam name="TSource">The type of the elements of source.</typeparam>
		/// <param name="source">A sequence containing elements.</param>
		/// <param name="item">The item to locate.</param>        
		/// <returns>The index of the entry if it was found in the sequence; otherwise, -1.</returns>
		public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource item)
		{
			return IndexOf<TSource>(source, item, null);
		}

		// This provides a useful extension-like method to find the index of and item from IEnumerable<T>
		// This was based off of the Enumerable.Count<T> extension method.
		/// <summary>
		/// Returns the index of an item in a sequence.
		/// </summary>
		/// <typeparam name="TSource">The type of the elements of source.</typeparam>
		/// <param name="source">A sequence containing elements.</param>
		/// <param name="item">The item to locate.</param>
		/// <param name="itemComparer">The item equality comparer to use.  Pass null to use the default comparer.</param>
		/// <returns>The index of the entry if it was found in the sequence; otherwise, -1.</returns>
		public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource item, IEqualityComparer<TSource> itemComparer)
		{
			if (source == null)
			{
				throw new ArgumentNullException("source");
			}

			IList<TSource> listOfT = source as IList<TSource>;
			if (listOfT != null)
			{
				return listOfT.IndexOf(item);
			}

			IList list = source as IList;
			if (list != null)
			{
				return list.IndexOf(item);
			}

			if (itemComparer == null)
			{
				itemComparer = EqualityComparer<TSource>.Default;
			}

			int i = 0;
			foreach (TSource possibleItem in source)
			{
				if (itemComparer.Equals(item, possibleItem))
				{
					return i;
				}
				i++;
			}
			return -1;
		}
	}
}

namespace DynamicData.HashSet
{


	/// <summary>
	/// Represents an observable set of values.
	/// </summary>
	/// <typeparam name="T">The type of elements in the hash set.</typeparam>    
	public sealed class ObservableHashSet<T> : ISet<T>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
	{
		private SimpleMonitor _monitor = new SimpleMonitor();
		private readonly HashSet<T> hashSet;

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableHashSet&lt;T&gt;"/> class.
		/// </summary>
		public ObservableHashSet()
		{
			this.hashSet = new HashSet<T>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableHashSet&lt;T&gt;"/> class.
		/// </summary>
		/// <param name="collection">The collection whose elements are copied to the new set.</param>
		public ObservableHashSet(IEnumerable<T> collection)
		{
			this.hashSet = new HashSet<T>(collection);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableHashSet&lt;T&gt;"/> class.
		/// </summary>
		/// <param name="comparer">The IEqualityComparer&lt;T&gt; implementation to use when comparing values in the set, or null to use the default EqualityComparer&lt;T&gt; implementation for the set type.</param>
		public ObservableHashSet(IEqualityComparer<T> comparer)
		{
			this.hashSet = new HashSet<T>(comparer);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableHashSet&lt;T&gt;"/> class.
		/// </summary>
		/// <param name="collection">The collection whose elements are copied to the new set.</param>
		/// <param name="comparer">The IEqualityComparer&lt;T&gt; implementation to use when comparing values in the set, or null to use the default EqualityComparer&lt;T&gt; implementation for the set type.</param>
		public ObservableHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
		{
			this.hashSet = new HashSet<T>(collection, comparer);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			if (this._monitor != null)
			{
				this._monitor.Dispose();
				this._monitor = null;
			}
		}

		#region Properties        

		/// <summary>
		/// The property names used with INotifyPropertyChanged.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "A container for constants used with INotifyPropertyChanged.")]
		public static class PropertyNames
		{
			public const string Count = "Count";
			public const string IsReadOnly = "IsReadOnly";
		}


		/// <summary>
		/// Gets the IEqualityComparer&lt;T&gt; object that is used to determine equality for the values in the set.
		/// </summary>
		public IEqualityComparer<T> Comparer
		{
			get { return this.hashSet.Comparer; }
		}


		/// <summary>
		/// Gets the number of elements contained in the <see cref="ObservableHashSet&lt;T&gt;"/>.
		/// </summary>
		/// <returns>
		/// The number of elements contained in the <see cref="ObservableHashSet&lt;T&gt;"/>.
		///   </returns>
		public int Count
		{
			get { return this.hashSet.Count; }
		}

		/// <summary>
		/// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
		/// </summary>
		/// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false.
		///   </returns>
		bool ICollection<T>.IsReadOnly
		{
			get { return ((ICollection<T>)this.hashSet).IsReadOnly; }
		}

		#endregion

		#region Events

		/// <summary>
		/// Raised when the collection changes.
		/// </summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			if (this.CollectionChanged != null)
			{
				using (this.BlockReentrancy())
				{
					this.CollectionChanged(this, e);
				}
			}
		}

		/// <summary>
		/// Raised when a property value changes.
		/// </summary>       
		public event PropertyChangedEventHandler PropertyChanged;

		private void RaisePropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Adds the specified element to a set.
		/// </summary>
		/// <param name="item">The element to add to the set.</param>
		/// <returns>true if the element is added to the <see cref="ObservableHashSet&lt;T&gt;"/> object; false if the element is already present.</returns>
		public bool Add(T item)
		{
			this.CheckReentrancy();

			bool wasAdded = this.hashSet.Add(item);

			if (wasAdded)
			{
				int index = this.hashSet.IndexOf(item);
				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
				this.RaisePropertyChanged(PropertyNames.Count);
			}

			return wasAdded;
		}

		/// <summary>
		/// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </summary>
		/// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
		/// <exception cref="T:System.NotSupportedException">
		/// The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
		///   </exception>
		void ICollection<T>.Add(T item)
		{
			this.Add(item);
		}

		/// <summary>
		/// Removes all elements from a <see cref="ObservableHashSet&lt;T&gt;"/> object.
		/// </summary>        
		public void Clear()
		{
			this.CheckReentrancy();

			if (this.hashSet.Count > 0)
			{
				this.hashSet.Clear();

				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				this.RaisePropertyChanged(PropertyNames.Count);
			}
		}

		/// <summary>
		/// Determines whether a <see cref="ObservableHashSet&lt;T&gt;"/> object contains the specified element.
		/// </summary>
		/// <param name="item">The element to locate in the <see cref="ObservableHashSet&lt;T&gt;"/> object.</param>
		/// <returns>true if the <see cref="ObservableHashSet&lt;T&gt;"/> object contains the specified element; otherwise, false.</returns>
		public bool Contains(T item)
		{
			return this.hashSet.Contains(item);
		}


		/// <summary>
		/// Copies the elements of a <see cref="ObservableHashSet&lt;T&gt;"/> collection to an array.
		/// </summary>
		/// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="ObservableHashSet&lt;T&gt;"/> object. The array must have zero-based indexing.</param>
		public void CopyTo(T[] array)
		{
			this.hashSet.CopyTo(array);
		}

		/// <summary>
		/// Copies the elements of a <see cref="ObservableHashSet&lt;T&gt;"/> collection to an array.
		/// </summary>
		/// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="ObservableHashSet&lt;T&gt;"/> object. The array must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			this.hashSet.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Copies the elements of a <see cref="ObservableHashSet&lt;T&gt;"/> collection to an array.
		/// </summary>
		/// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="ObservableHashSet&lt;T&gt;"/> object. The array must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
		/// <param name="count">The number of elements to copy to array.</param>
		public void CopyTo(T[] array, int arrayIndex, int count)
		{
			this.hashSet.CopyTo(array, arrayIndex, count);
		}

		/// <summary>
		/// Removes all elements in the specified collection from the current <see cref="ObservableHashSet&lt;T&gt;"/> object.
		/// </summary>
		/// <param name="other">The collection of items to remove from the <see cref="ObservableHashSet&lt;T&gt;"/> object.</param>        
		public void ExceptWith(IEnumerable<T> other)
		{

			this.CheckReentrancy();

			// I locate items in other that are in the hashset
			var removedItems = other.Where(x => this.hashSet.Contains(x)).ToList();

			this.hashSet.ExceptWith(other);

			if (removedItems.Count > 0)
			{
				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems));
				this.RaisePropertyChanged(PropertyNames.Count);
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through a <see cref="ObservableHashSet&lt;T&gt;"/>.
		/// </summary>
		/// <returns>A <see cref="ObservableHashSet&lt;T&gt;"/>.Enumerator object for the <see cref="ObservableHashSet&lt;T&gt;"/> object.</returns>
		public IEnumerator<T> GetEnumerator()
		{
			return this.hashSet.GetEnumerator();
		}

		/// <summary>
		/// Modifies the current <see cref="ObservableHashSet&lt;T&gt;"/> object to contain only elements that are present in that object and in the specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object.</param>
		public void IntersectWith(IEnumerable<T> other)
		{
			this.CheckReentrancy();

			// I locate the items in the hashset that are not in other
			var removedItems = this.hashSet.Where(x => !other.Contains(x)).ToList();

			this.hashSet.IntersectWith(other);

			if (removedItems.Count > 0)
			{
				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems));
				this.RaisePropertyChanged(PropertyNames.Count);
			}
		}

		/// <summary>
		/// Determines whether a <see cref="ObservableHashSet&lt;T&gt;"/> object is a proper subset of the specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object.</param>
		/// <returns>true if the <see cref="ObservableHashSet&lt;T&gt;"/> object is a proper subset of other; otherwise, false.</returns>
		public bool IsProperSubsetOf(IEnumerable<T> other)
		{
			return this.hashSet.IsProperSubsetOf(other);
		}

		/// <summary>
		/// Determines whether a <see cref="ObservableHashSet&lt;T&gt;"/> object is a proper subset of the specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object. </param>
		/// <returns>true if the <see cref="ObservableHashSet&lt;T&gt;"/> object is a proper superset of other; otherwise, false.</returns>
		public bool IsProperSupersetOf(IEnumerable<T> other)
		{
			return this.hashSet.IsProperSupersetOf(other);
		}

		/// <summary>
		/// Determines whether a <see cref="ObservableHashSet&lt;T&gt;"/> object is a subset of the specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object. </param>
		/// <returns>true if the <see cref="ObservableHashSet&lt;T&gt;"/> object is a subset of other; otherwise, false.</returns>
		public bool IsSubsetOf(IEnumerable<T> other)
		{
			return this.hashSet.IsSubsetOf(other);
		}

		/// <summary>
		/// Determines whether a <see cref="ObservableHashSet&lt;T&gt;"/> object is a superset of the specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object. </param>
		/// <returns>true if the <see cref="ObservableHashSet&lt;T&gt;"/> object is a superset of other; otherwise, false.</returns>
		public bool IsSupersetOf(IEnumerable<T> other)
		{
			return this.hashSet.IsSupersetOf(other);
		}

		/// <summary>
		/// Determines whether the current <see cref="ObservableHashSet&lt;T&gt;"/> object and a specified collection share common elements.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object. </param>
		/// <returns>true if the <see cref="ObservableHashSet&lt;T&gt;"/> object and other share at least one common element; otherwise, false.</returns>
		public bool Overlaps(IEnumerable<T> other)
		{
			return this.hashSet.Overlaps(other);
		}

		/// <summary>
		/// Removes the specified element from a <see cref="ObservableHashSet&lt;T&gt;"/> object.
		/// </summary>
		/// <param name="item">The element to remove.</param>
		/// <returns>true if the element is successfully found and removed; otherwise, false. This method returns false if item is not found in the <see cref="ObservableHashSet&lt;T&gt;"/> object.</returns>
		public bool Remove(T item)
		{
			int index = this.hashSet.IndexOf(item);
			bool wasRemoved = this.hashSet.Remove(item);

			if (wasRemoved)
			{

				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
				this.RaisePropertyChanged(PropertyNames.Count);
			}

			return wasRemoved;
		}

		/// <summary>
		/// Determines whether a <see cref="ObservableHashSet&lt;T&gt;"/> object and the specified collection contain the same elements.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object. </param>
		/// <returns>true if the <see cref="ObservableHashSet&lt;T&gt;"/> object is equal to other; otherwise, false.</returns>
		public bool SetEquals(IEnumerable<T> other)
		{
			return this.hashSet.SetEquals(other);
		}

		/// <summary>
		/// Modifies the current <see cref="ObservableHashSet&lt;T&gt;"/> object to contain only elements that are present either in that object or in the specified collection, but not both.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object.</param>
		public void SymmetricExceptWith(IEnumerable<T> other)
		{
			this.CheckReentrancy();

			// I locate the items in other that are not in the hashset
			var addedItems = other.Where(x => !this.hashSet.Contains(x)).ToList();

			// I locate items in other that are in the hashset
			var removedItems = other.Where(x => this.hashSet.Contains(x)).ToList();

			this.hashSet.SymmetricExceptWith(other);

			if (removedItems.Count > 0)
			{
				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems));
				this.RaisePropertyChanged(PropertyNames.Count);
			}

			if (addedItems.Count > 0)
			{
				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedItems));
			}

			if (removedItems.Count > 0 || addedItems.Count > 0)
			{
				this.RaisePropertyChanged(PropertyNames.Count);
			}
		}

		/// <summary>
		/// Sets the capacity of a <see cref="ObservableHashSet&lt;T&gt;"/> object to the actual number of elements it contains, rounded up to a nearby, implementation-specific value.
		/// </summary>
		public void TrimExcess()
		{
			this.hashSet.TrimExcess();
		}

		/// <summary>
		/// Modifies the current <see cref="ObservableHashSet&lt;T&gt;"/> object to contain all elements that are present in itself, the specified collection, or both.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="ObservableHashSet&lt;T&gt;"/> object.</param>
		public void UnionWith(IEnumerable<T> other)
		{
			this.CheckReentrancy();

			// I locate the items in other that are not in the hashset
			var addedItems = other.Where(x => !this.hashSet.Contains(x)).ToList();

			this.hashSet.UnionWith(other);

			if (addedItems.Count > 0)
			{
				this.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedItems));
				this.RaisePropertyChanged(PropertyNames.Count);
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
		/// </returns>
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((System.Collections.IEnumerable)this.hashSet).GetEnumerator();
		}

		#endregion

		#region Reentrancy Methods

		private IDisposable BlockReentrancy()
		{
			this._monitor.Enter();
			return this._monitor;
		}

		private void CheckReentrancy()
		{
			if ((this._monitor.Busy && (this.CollectionChanged != null)) && (this.CollectionChanged.GetInvocationList().Length > 1))
			{
				throw new InvalidOperationException("There are additional attempts to change this hash set during a CollectionChanged event.");
			}
		}

		#endregion

		#region Private Classes

		private class SimpleMonitor : IDisposable
		{
			private int _busyCount;

			public void Dispose()
			{
				this._busyCount--;
			}

			public void Enter()
			{
				this._busyCount++;
			}

			public bool Busy => (this._busyCount > 0);
		}


		#endregion
	}
}
