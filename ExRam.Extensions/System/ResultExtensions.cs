﻿using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using LanguageExt.Common;

namespace LanguageExt
{
    public static class ResultExtensions
    {
        [Pure]
        public static Result<B> Bind<A, B>(this Result<A> result, Func<A, Result<B>> f)
        {
            return result.Match(
                f,
                ex => new Result<B>(ex));
        }

        [Pure]
        public static Task<Result<B>> BindAsync<A, B>(this Result<A> result, Func<A, Task<Result<B>>> f)
        {
            return result.Match(
                f,
                async ex => new Result<B>(ex));
        }

        [Pure]
        public static Option<A> TryGetValue<A>(this Result<A> result)
        {
            return result.Match(
                _ => _,
                ex => Option<A>.None);
        }

        [Pure]
        public static Option<T> Handle<T>(this Result<T> result, Action<Exception> handler)
        {
            return result.Match(
                _ => _,
                ex =>
                {
                    handler(ex);

                    return Option<T>.None;
                });
        }

        [Pure]
        public static async Task<Option<T>> Handle<T>(this Task<Result<T>> resultTask, Action<Exception> handler)
        {
            return (await resultTask).Handle(handler);
        }

        public static Either<Exception, T> ToEither<T>(this Result<T> result)
        {
            return result.Match(
                Prelude.Right<Exception, T>,
                Prelude.Left<Exception, T>);
        }

        public static EitherAsync<Exception, T> ToEither<T>(this Task<Result<T>> result)
        {
            return result
                .Map(r => r.Match(
                    Prelude.Right<Exception, T>,
                    Prelude.Left<Exception, T>))
                .ToAsync();
        }

        public static Either<L, R> ForceLeft<L, R>(this Either<L, R> either, Func<R, L> force)
        {
            return either.Bind(
                _ => Prelude.Left<L, R>(force(_)));
        }

        public static Either<L, R> ForceRight<L, R>(this Either<L, R> either, Func<L, R> force)
        {
            return either.BindLeft(
                _ => Prelude.Right<L, R>(force(_)));
        }
    }
}