// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET9_0_OR_GREATER
#pragma warning disable CS9216 // Casting Lock to object will use monitor-based locking - intentional for polyfill
#pragma warning disable CA2002 // Normally locking/Monitoring on `this` is indeed a bad idea, but here it's intentional.

namespace System.Threading;

// Obviously, this isn't really a proper polyfill, but all we need is something with enough functionality to fit into
// a lock() statement. That allows us to just write one set of code for all target frameworks, and it just compiles
// against whichever Lock implementation is available.
internal sealed class Lock
{
    public void Enter()
        => Monitor.Enter(this);

    public Scope EnterScope()
    {
        Enter();
        return new(this);
    }

    public void Exit()
        => Monitor.Exit(this);

    public readonly ref struct Scope(Lock owner)
    {
        public void Dispose()
            => owner.Exit();
    }
}
#endif
