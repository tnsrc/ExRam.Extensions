﻿using System.Diagnostics.Contracts;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace System.Reactive.Linq
{
    public static partial class ObservableExtensions
    {
        public static IObservable<TAccumulate> ScanAsync<TSource, TAccumulate>(this IObservable<TSource> source, TAccumulate seed, Func<TAccumulate, TSource, Task<TAccumulate>> accumulator)
        {
            Contract.Requires(source != null);
            Contract.Requires(accumulator != null);

            return source
                .Scan(Task.FromResult(seed), async (currentTask, value) => await accumulator(await currentTask, value))
                .SelectMany(x => x.ToObservable());
        }
    }
}
