// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

global using System;
global using System.Collections;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Collections.Specialized;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Linq;
global using System.Linq.Expressions;
global using System.Reactive;
global using System.Reactive.Concurrency;
global using System.Reactive.Disposables;
global using System.Reactive.Linq;
global using System.Reactive.Subjects;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using System.Threading.Tasks;

global using DynamicData.Binding;
global using DynamicData.Cache.Internal;
global using DynamicData.Diagnostics;
global using DynamicData.Internal;
global using DynamicData.Kernel;
global using DynamicData.List.Internal;
global using DynamicData.List.Linq;
global using DynamicData.Operators;

[assembly: InternalsVisibleTo("DynamicData.Tests")]
