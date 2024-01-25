// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace DynamicData;

/// <summary>
/// Options for TransformAsync and TransformSafeAsync.
/// </summary>
/// <param name="MaximumConcurrency">The maximum number of tasks in flight at once.</param>
/// <param name="TransformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
public record struct TransformAsyncOptions(int? MaximumConcurrency, bool TransformOnRefresh)
{
    /// <summary>
    /// The default transform async option values, with is unlimited concurrency and do not transform on reset.
    /// </summary>
    /// <returns>A TransformAsyncOptions object.</returns>
    public static readonly TransformAsyncOptions Default = new(null, false);
}
