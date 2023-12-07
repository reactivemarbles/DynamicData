// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// A request object for virtualisation.
/// </summary>
public class VirtualRequest : IEquatable<IVirtualRequest>, IVirtualRequest
{
    /// <summary>
    /// The default request value.
    /// </summary>
    public static readonly VirtualRequest Default = new(0, 25);

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualRequest"/> class.
    /// </summary>
    /// <param name="startIndex">The start index.</param>
    /// <param name="size">The size.</param>
    public VirtualRequest(int startIndex, int size)
    {
        Size = size;
        StartIndex = startIndex;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualRequest"/> class.
    /// </summary>
    public VirtualRequest()
    {
    }

    /// <summary>
    /// Gets the start index size comparer.
    /// </summary>
    /// <value>
    /// The start index size comparer.
    /// </value>
    public static IEqualityComparer<IVirtualRequest?> StartIndexSizeComparer { get; } = new StartIndexSizeEqualityComparer();

    /// <summary>
    /// Gets the maximum number of items to return.
    /// </summary>
    public int Size { get; } = 25;

    /// <summary>
    /// Gets the first index in the virualised list.
    /// </summary>
    public int StartIndex { get; }

    /// <inheritdoc />
    public bool Equals(IVirtualRequest? other) => StartIndexSizeComparer.Equals(this, other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IVirtualRequest item && Equals(item);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (StartIndex * 397) ^ Size;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"StartIndex: {StartIndex}, Size: {Size}";

    private sealed class StartIndexSizeEqualityComparer : IEqualityComparer<IVirtualRequest?>
    {
        public bool Equals(IVirtualRequest? x, IVirtualRequest? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.StartIndex == y.StartIndex && x.Size == y.Size;
        }

        public int GetHashCode(IVirtualRequest? obj)
        {
            if (obj is null)
            {
                return 0;
            }

            unchecked
            {
                return (obj.StartIndex * 397) ^ obj.Size;
            }
        }
    }
}
