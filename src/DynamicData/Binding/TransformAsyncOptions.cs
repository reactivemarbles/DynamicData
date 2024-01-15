// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace DynamicData.Binding;

/// <summary>
/// Options for TransformAsync and TransformSafeAsync.
/// </summary>
/// <param name="MaximumConcurrency">The maximum number of tasks in flight at once.</param>
/// <param name="TransformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
public record struct TransformAsyncOptions(int? MaximumConcurrency = null, bool TransformOnRefresh = false)
{
    /// <summary>
    /// Options with WithTransformOnRefresh = true.
    /// </summary>
    public static readonly TransformAsyncOptions WithTransformOnRefresh = new(TransformOnRefresh: true);

    /// <summary>
    /// Specify maximum concurrency only.
    /// </summary>
    /// <param name="maximumConcurrency">The maximum concurrency.</param>
    /// <returns>A TransformAsyncOptions object.</returns>
    public static TransformAsyncOptions WithMaximumConcurrency(int maximumConcurrency) => new(maximumConcurrency);
}
