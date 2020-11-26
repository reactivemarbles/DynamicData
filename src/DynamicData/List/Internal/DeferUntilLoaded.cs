// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class DeferUntilLoaded<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;

        public DeferUntilLoaded(IObservable<IChangeSet<T>> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return _source.MonitorStatus().Where(status => status == ConnectionStatus.Loaded).Take(1).Select(_ => new ChangeSet<T>()).Concat(_source).NotEmpty();
        }
    }
}