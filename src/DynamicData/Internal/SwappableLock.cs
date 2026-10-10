// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

internal ref struct SwappableLock
{
    public static SwappableLock CreateAndEnter(Lock gate)
    {
        gate.Enter();
        return new SwappableLock { _gate = gate };
    }

    public void SwapTo(Lock gate)
    {
        if (_gate is null)
            throw new InvalidOperationException("Lock is not initialized");

        gate.Enter();
        _gate.Exit();
        _gate = gate;
    }

    public void Dispose()
    {
        if (_gate is not null)
        {
            _gate.Exit();
            _gate = null;
        }
    }

    private Lock? _gate;
}
