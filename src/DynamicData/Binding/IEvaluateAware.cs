// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

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
