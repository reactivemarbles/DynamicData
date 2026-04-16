// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NETCOREAPP3_0_OR_GREATER
using System.Numerics;
#endif
using System.Runtime.CompilerServices;

namespace DynamicData.Internal;

/// <summary>
/// <para>
/// A compact bitset backed by a <see langword="long"/>[] array that tracks active slots
/// (e.g., sub-queues with pending items). Provides O(1) set/clear operations and fast
/// highest/lowest-bit lookup via hardware intrinsics (LZCNT/TZCNT) for LIFO/FIFO iteration.
/// </para>
/// <para>
/// Each <see langword="long"/> holds 64 bits. A slot index maps to a word and bit position:
/// <c>word = index / 64</c>, <c>bit = index % 64</c>. The backing array grows automatically
/// via <see cref="EnsureCapacity"/> but never shrinks. Callers that need compaction should
/// create a new instance.
/// </para>
/// </summary>
internal struct Bitset
{
    private const int BitsPerWord = 64;
    private const int WordShift = 6;
    private const int BitMask = BitsPerWord - 1;

    private long[] _words;

    /// <summary>Initializes a new instance of the <see cref="Bitset"/> struct with capacity for 64 slots.</summary>
    public Bitset() => _words = [0];

    /// <summary>Gets the number of bits currently set.</summary>
    public int Count { get; private set; }

    /// <summary>Sets the bit at <paramref name="index"/>, marking the slot as active.</summary>
    /// <param name="index">The zero-based slot index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index)
    {
        EnsureCapacity(index);
        ref var word = ref _words[index >> WordShift];
        var mask = 1L << (index & BitMask);
        if ((word & mask) == 0)
        {
            word |= mask;
            Count++;
        }
    }

    /// <summary>Clears the bit at <paramref name="index"/>, marking the slot as inactive.</summary>
    /// <param name="index">The zero-based slot index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int index)
    {
        if ((index >> WordShift) >= _words.Length)
        {
            return;
        }

        ref var word = ref _words[index >> WordShift];
        var mask = 1L << (index & BitMask);
        if ((word & mask) != 0)
        {
            word &= ~mask;
            Count--;
        }
    }

    /// <summary>Returns <see langword="true"/> if the bit at <paramref name="index"/> is set.</summary>
    /// <param name="index">The zero-based slot index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsSet(int index)
    {
        var wordIndex = index >> WordShift;
        if (wordIndex >= _words.Length)
        {
            return false;
        }

        return (_words[wordIndex] & (1L << (index & BitMask))) != 0;
    }

    /// <summary>
    /// <para>
    /// Finds the highest set bit (for LIFO iteration) and returns its index,
    /// or -1 if no bits are set.
    /// </para>
    /// <para>
    /// Scans words from highest to lowest. Within each word, the leading zero count
    /// intrinsic (LZCNT on x86, CLZ on ARM) locates the most significant set bit
    /// in a single CPU instruction.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int FindHighest()
    {
        var words = _words;
        if (words.Length == 1)
        {
            var w0 = words[0];
            if (w0 == 0) return -1;
#if NETCOREAPP3_0_OR_GREATER
            return 63 - BitOperations.LeadingZeroCount((ulong)w0);
#else
            return HighestSetBit(w0);
#endif
        }

        for (var w = words.Length - 1; w >= 0; w--)
        {
            var word = words[w];
            if (word != 0)
            {
#if NETCOREAPP3_0_OR_GREATER
                var bitIndex = 63 - BitOperations.LeadingZeroCount((ulong)word);
#else
                var bitIndex = HighestSetBit(word);
#endif
                return (w << WordShift) | bitIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// <para>
    /// Finds the lowest set bit (for FIFO iteration) and returns its index,
    /// or -1 if no bits are set.
    /// </para>
    /// <para>
    /// Scans words from lowest to highest. Within each word, the trailing zero count
    /// intrinsic (TZCNT on x86, CTZ on ARM) locates the least significant set bit
    /// in a single CPU instruction.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int FindLowest()
    {
        var words = _words;
        for (var w = 0; w < words.Length; w++)
        {
            var word = words[w];
            if (word != 0)
            {
#if NETCOREAPP3_0_OR_GREATER
                var bitIndex = BitOperations.TrailingZeroCount((ulong)word);
#else
                var bitIndex = LowestSetBit(word);
#endif
                return (w << WordShift) | bitIndex;
            }
        }

        return -1;
    }

    /// <summary>Returns <see langword="true"/> if any bit is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasAny() => Count > 0;

    /// <summary>Clears all bits in every word.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearAll()
    {
        if (_words.Length == 1)
        {
            _words[0] = 0;
        }
        else
        {
            Array.Clear(_words, 0, _words.Length);
        }

        Count = 0;
    }

    /// <summary>Grows the backing array if needed so that <paramref name="index"/> is addressable. Called implicitly by <see cref="Set"/>.</summary>
    /// <param name="index">The zero-based slot index that must be representable.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int index)
    {
        var wordIndex = index >> WordShift;
        if (wordIndex >= _words.Length)
        {
            Array.Resize(ref _words, wordIndex + 1);
        }
    }

    /// <summary>
    /// Shrinks the backing array to the minimum size needed to represent all set bits.
    /// Reclaims memory from words that are entirely zero at the end of the array.
    /// Always retains at least one word.
    /// </summary>
    public void Compact()
    {
        var needed = 1;
        for (var w = _words.Length - 1; w >= 1; w--)
        {
            if (_words[w] != 0)
            {
                needed = w + 1;
                break;
            }
        }

        if (needed < _words.Length)
        {
            Array.Resize(ref _words, needed);
        }
    }

#if !NETCOREAPP3_0_OR_GREATER
    private static int HighestSetBit(long value)
    {
        var bit = 0;
        for (var v = (ulong)value; v > 1; v >>= 1)
        {
            bit++;
        }

        return bit;
    }

    private static int LowestSetBit(long value)
    {
        var bit = 0;
        var v = (ulong)value;
        while ((v & 1) == 0)
        {
            v >>= 1;
            bit++;
        }

        return bit;
    }
#endif
}
