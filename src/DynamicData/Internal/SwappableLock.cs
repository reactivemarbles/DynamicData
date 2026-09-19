// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif
#if NET9_0_OR_GREATER

/// <summary>
/// Represents the SwappableLock value.
/// </summary>
internal ref struct SwappableLock
{
    /// <summary>
    /// Executes the CreateAndEnter operation.
    /// </summary>
    /// <param name="gate">The gate value.</param>
    /// <returns>The result of the operation.</returns>
    public static SwappableLock CreateAndEnter(Lock gate)
    {
        gate.Enter();
        return new SwappableLock { _gate = gate };
    }

    /// <summary>
    /// Executes the SwapTo operation.
    /// </summary>
    /// <param name="gate">The gate value.</param>
    public void SwapTo(Lock gate)
    {
        if (_gate is null)
            throw new InvalidOperationException("Lock is not initialized");
        gate.Enter();
        _gate.Exit();
        _gate = gate;
    }

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_gate is not null)
        {
            _gate.Exit();
            _gate = null;
        }
    }

    /// <summary>
    /// The _gate field.
    /// </summary>
    private Lock? _gate;
}
#else

/// <summary>
/// Represents the SwappableLock value.
/// </summary>
internal ref struct SwappableLock
{
    /// <summary>
    /// Executes the CreateAndEnter operation.
    /// </summary>
    /// <param name="gate">The gate value.</param>
    /// <returns>The result of the operation.</returns>
    public static SwappableLock CreateAndEnter(object gate)
    {
        var result = new SwappableLock()
        {
            _gate = gate
        };

        Monitor.Enter(gate, ref result._hasLock);

        return result;
    }

    /// <summary>
    /// Executes the SwapTo operation.
    /// </summary>
    /// <param name="gate">The gate value.</param>
    public void SwapTo(object gate)
    {
        if (_gate is null)
            throw new InvalidOperationException("Lock is not initialized");

        var hasNewLock = false;
        Monitor.Enter(gate, ref hasNewLock);

        if (_hasLock)
        {
            Monitor.Exit(_gate);
        }

        _hasLock = hasNewLock;
        _gate = gate;
    }

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_hasLock && (_gate is not null))
        {
            Monitor.Exit(_gate);
            _hasLock = false;
            _gate = null;
        }
    }

    /// <summary>
    /// The _hasLock field.
    /// </summary>
    private bool _hasLock;

    /// <summary>
    /// The _gate field.
    /// </summary>
    private object? _gate;
}

#endif
