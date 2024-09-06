// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// A set of changes which has occurred since the last reported change.
/// </summary>
/// <typeparam name="T">The type of the object.</typeparam>
public class ChangeSet<T> : List<Change<T>>, IChangeSet<T>
    where T : notnull
{
    /// <summary>
    /// An empty change set.
    /// </summary>
    public static readonly IChangeSet<T> Empty = new ChangeSet<T>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> class.
    /// </summary>
    public ChangeSet()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}" /> class.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <exception cref="ArgumentNullException">items.</exception>
    public ChangeSet(IEnumerable<Change<T>> items)
        : base(items)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity of the change set.</param>
    public ChangeSet(int capacity)
        : base(capacity)
    {
    }

    /// <summary>
    ///     Gets the number of additions.
    /// </summary>
    public int Adds
    {
        get
        {
            var adds = 0;
            foreach (var item in this)
            {
                switch (item.Reason)
                {
                    case ListChangeReason.Add:
                        adds++;
                        break;

                    case ListChangeReason.AddRange:
                        adds += item.Range.Count;
                        break;
                }
            }

            return adds;
        }
    }

    /// <summary>
    ///     Gets the number of moves.
    /// </summary>
    public int Moves => this.Count(c => c.Reason == ListChangeReason.Moved);

    /// <summary>
    ///     Gets the number of refreshes.
    /// </summary>
    public int Refreshes => this.Count(c => c.Reason == ListChangeReason.Refresh);

    /// <summary>
    ///     Gets the number of removes.
    /// </summary>
    public int Removes
    {
        get
        {
            var removes = 0;
            foreach (var item in this)
            {
                switch (item.Reason)
                {
                    case ListChangeReason.Remove:
                        removes++;
                        break;

                    case ListChangeReason.RemoveRange:
                    case ListChangeReason.Clear:
                        removes += item.Range.Count;
                        break;
                }
            }

            return removes;
        }
    }

    /// <summary>
    ///     Gets the number of updates.
    /// </summary>
    public int Replaced => this.Count(c => c.Reason == ListChangeReason.Replace);

    /// <summary>
    ///     Gets the total number if individual item changes.
    /// </summary>
    public int TotalChanges => Adds + Removes + Refreshes + Replaced + Moves;

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString() => $"ChangeSet<{typeof(T).Name}>. Count={Count}";
}
