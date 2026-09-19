// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Provides members for the VirtualChangeSet class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="virtualChangeSet">The virtualChangeSet value.</param>
/// <param name="response">The response value.</param>
internal sealed class VirtualChangeSet<T>(IChangeSet<T> virtualChangeSet, IVirtualResponse response) : IVirtualChangeSet<T>
    where T : notnull
{
    /// <summary>
    /// The _virtualChangeSet field.
    /// </summary>
    private readonly IChangeSet<T> _virtualChangeSet = virtualChangeSet ?? throw new ArgumentNullException(nameof(virtualChangeSet));

    /// <summary>
    /// Gets the Refreshes value.
    /// </summary>
    public int Refreshes => _virtualChangeSet.Refreshes;

    /// <summary>
    /// Gets the Response value.
    /// </summary>
    public IVirtualResponse Response { get; } = response ?? throw new ArgumentNullException(nameof(response));

    /// <summary>
    /// Gets the Adds value.
    /// </summary>
    int IChangeSet.Adds => _virtualChangeSet.Adds;

    /// <summary>
    /// Gets or sets the Capacity value.
    /// </summary>
    int IChangeSet.Capacity
    {
        get => _virtualChangeSet.Capacity;
        set => _virtualChangeSet.Capacity = value;
    }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    int IChangeSet.Count => _virtualChangeSet.Count;

    /// <summary>
    /// Gets the Moves value.
    /// </summary>
    int IChangeSet.Moves => _virtualChangeSet.Moves;

    /// <summary>
    /// Gets the Removes value.
    /// </summary>
    int IChangeSet.Removes => _virtualChangeSet.Removes;

    /// <summary>
    /// Gets the Replaced value.
    /// </summary>
    int IChangeSet<T>.Replaced => _virtualChangeSet.Replaced;

    /// <summary>
    /// Gets the TotalChanges value.
    /// </summary>
    int IChangeSet<T>.TotalChanges => _virtualChangeSet.TotalChanges;

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<Change<T>> GetEnumerator() => _virtualChangeSet.GetEnumerator();

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
