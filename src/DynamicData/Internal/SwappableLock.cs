// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
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

    public void Dispose()
    {
        if (_hasLock && (_gate is not null))
        {
            Monitor.Exit(_gate);
            _hasLock = false;
            _gate = null;
        }
    }

    private bool _hasLock;
    private object? _gate;
}
