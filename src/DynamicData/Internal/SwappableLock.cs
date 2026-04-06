// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

internal ref struct SwappableLock
{
    public static SwappableLock CreateAndEnter(object gate)
    {
        var result = new SwappableLock()
        {
            _gate = gate
        };

        Monitor.Enter(gate, ref result._hasLock);

        return result;
    }

#if NET9_0_OR_GREATER
    public static SwappableLock CreateAndEnter(Lock gate)
    {
        gate.Enter();
        return new SwappableLock() { _lockGate = gate };
    }
#endif

    public void SwapTo(object gate)
    {
        if (_gate is null)
            throw new InvalidOperationException("Lock is not initialized");

        var hasNewLock = false;
        Monitor.Enter(gate, ref hasNewLock);

        if (_hasLock)
            Monitor.Exit(_gate);

        _hasLock = hasNewLock;
        _gate = gate;
    }

#if NET9_0_OR_GREATER
    public void SwapTo(Lock gate)
    {
        if (_lockGate is null && _gate is null)
            throw new InvalidOperationException("Lock is not initialized");

        gate.Enter();

        if (_lockGate is not null)
            _lockGate.Exit();
        else if (_hasLock)
            Monitor.Exit(_gate!);

        _lockGate = gate;
        _hasLock = false;
        _gate = null;
    }
#endif

    public void Dispose()
    {
#if NET9_0_OR_GREATER
        if (_lockGate is not null)
        {
            _lockGate.Exit();
            _lockGate = null;
        }
        else
#endif
        if (_hasLock && (_gate is not null))
        {
            Monitor.Exit(_gate);
            _hasLock = false;
            _gate = null;
        }
    }

    private bool _hasLock;
    private object? _gate;

#if NET9_0_OR_GREATER
    private Lock? _lockGate;
#endif
}
