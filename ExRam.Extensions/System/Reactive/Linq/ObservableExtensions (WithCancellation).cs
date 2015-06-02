﻿// (c) Copyright 2014 ExRam GmbH & Co. KG http://www.exram.de
//
// Licensed using Microsoft Public License (Ms-PL)
// Full License description can be found in the LICENSE
// file.

using System.Diagnostics.Contracts;
using System.Reactive.Disposables;
using System.Threading;

namespace System.Reactive.Linq
{
    public static partial class ObservableExtensions
    {
        public static IObservable<T> WithCancellation<T>(Func<CancellationToken, IObservable<T>> observableFactory)
        {
            Contract.Requires(observableFactory != null);

            return Observable
                .Using(
                    () => new CancellationDisposable(),
                    cts => observableFactory(cts.Token));
        }
    }
}