// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif

/// <summary>
/// Implement on an object and use in conjunction with InvokeEvaluate operator
/// to make an object aware of any evaluates.
/// </summary>
public interface IEvaluateAware
{
    /// <summary>
    /// Refresh method.
    /// </summary>
    void Evaluate();
}
