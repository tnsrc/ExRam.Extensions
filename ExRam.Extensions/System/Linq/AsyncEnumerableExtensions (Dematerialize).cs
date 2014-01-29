﻿using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reactive;

namespace System.Linq
{
    public static partial class AsyncEnumerableExtensions
    {
        public static IAsyncEnumerable<TSource> Dematerialize<TSource>(this IAsyncEnumerable<Notification<TSource>> enumerable)
        {
            Contract.Requires(enumerable != null);

            return AsyncEnumerable2.Create(() =>
            {
                var e = enumerable.GetEnumerator();

                return AsyncEnumeratorEx.Create(
                    async (ct) =>
                    {
                        if (await e.MoveNext(ct))
                        {
                            var current = e.Current;

                            if (current.HasValue)
                                return current.Value;

                            if (current.Exception != null)
                                throw current.Exception;
                        }

                        return Maybe<TSource>.Null;
                    },
                    e.Dispose);
            });
        }
    }
}
