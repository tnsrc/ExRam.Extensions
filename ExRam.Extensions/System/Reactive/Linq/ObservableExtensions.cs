﻿// (c) Copyright 2014 ExRam GmbH & Co. KG http://www.exram.de
//
// Licensed using Microsoft Public License (Ms-PL)
// Full License description can be found in the LICENSE
// file.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.SomeHelp;

namespace System.Reactive.Linq
{
    public static partial class ObservableExtensions
    {
        public static IObservable<object> Box<T>(this IObservable<T> source) where T : struct
        {
            Contract.Requires(source != null);

            return source.Select(x => (object)x);
        }

        public static IObservable<Tuple<TSource1, TSource2>> CombineLatest<TSource1, TSource2>(this IObservable<TSource1> first, IObservable<TSource2> second)
        {
            Contract.Requires(first != null);
            Contract.Requires(second != null);

            return first.CombineLatest(second, Tuple.Create);
        }

        public static IObservable<Tuple<TSource1, TSource2, TSource3>> CombineLatest<TSource1, TSource2, TSource3>(this IObservable<TSource1> source1, IObservable<TSource2> source2, IObservable<TSource3> source3)
        {
            Contract.Requires(source1 != null);
            Contract.Requires(source2 != null);
            Contract.Requires(source3 != null);

            return source1.CombineLatest(source2, source3, Tuple.Create);
        }

        public static IObservable<Tuple<TSource1, TSource2, TSource3, TSource4>> CombineLatest<TSource1, TSource2, TSource3, TSource4>(this IObservable<TSource1> source1, IObservable<TSource2> source2, IObservable<TSource3> source3, IObservable<TSource4> source4)
        {
            Contract.Requires(source1 != null);
            Contract.Requires(source2 != null);
            Contract.Requires(source3 != null);
            Contract.Requires(source4 != null);

            return source1.CombineLatest(source2, source3, source4, Tuple.Create);
        }

        public static IObservable<T> Concat<T>(this IObservable<T> source, Func<Option<T>, IObservable<T>> continuationSelector)
        {
            Contract.Requires(source != null);
            Contract.Requires(continuationSelector != null);

            return Observable.Create<T>(obs =>
            {
                var lastValue = Option<T>.None;
                var subscription = new SerialDisposable();
                var firstSubscription = new SingleAssignmentDisposable();

                subscription.Disposable = firstSubscription;

                firstSubscription.Disposable = source.Subscribe(
                    value =>
                    {
                        lastValue = value;
                        obs.OnNext(value);
                    },
                    ex =>
                    {
                        subscription.Dispose();
                        obs.OnError(ex);
                    },
                    () =>
                    {
                        subscription.Disposable.Dispose();
                        subscription.Disposable = continuationSelector(lastValue).Subscribe(obs);
                    });

                return subscription;
            });
        }

        public static IAsyncEnumerable<T> Current<T>(this IObservable<T> source)
        {
            Contract.Requires(source != null);

            return AsyncEnumerable
                .Using(
                    () => new ReplaySubject<Notification<T>>(1),
                    subject => AsyncEnumerable
                        .Using(
                            () => source
                                .Materialize()
                                .Multicast(subject)
                                .Connect(),
                            _ => AsyncEnumerable
                                .Repeat(Unit.Default)
                                .SelectMany(unit => subject
                                    .FirstAsync()
                                    .ToAsyncEnumerable())
                                .Dematerialize()));
        }

        public static IObservable<T> Debounce<T>(this IObservable<T> source, TimeSpan debounceInterval) where T : struct
        {
            Contract.Requires(source != null);

            return source.Debounce(debounceInterval, false, Scheduler.Default);
        }

        public static IObservable<T> Debounce<T>(this IObservable<T> source, TimeSpan debounceInterval, IScheduler scheduler) where T : struct
        {
            Contract.Requires(source != null);

            return source.Debounce(debounceInterval, false, scheduler);
        }

        public static IObservable<T> Debounce<T>(this IObservable<T> source, TimeSpan debounceInterval, bool emitLatestValue) where T : struct
        {
            Contract.Requires(source != null);

            return source.Debounce(debounceInterval, emitLatestValue, Scheduler.Default);
        }

        public static IObservable<T> Debounce<T>(this IObservable<T> source, TimeSpan debounceInterval, bool emitLatestValue, IScheduler scheduler) where T : struct
        {
            Contract.Requires(source != null);

            return Observable
                .Using(
                    () => new SerialDisposable(),
                    serial => Observable
                        .Create<T>(obs =>
                        {
                            var isCompleted = false;
                            var isDebouncing = false;
                            var syncRoot = new object();
                            Option<T> maybeLatestValue;

                            return source
                                .Subscribe(
                                    value =>
                                    {
                                        lock (syncRoot)
                                        {
                                            if (!isDebouncing)
                                            {
                                                isDebouncing = true;

                                                maybeLatestValue = Option<T>.None;
                                                obs.OnNext(value);

                                                serial.Disposable = scheduler.Schedule(value, debounceInterval, (self, state) =>
                                                {
                                                    lock (syncRoot)
                                                    {
                                                        isDebouncing = false;

                                                        if (!isCompleted && emitLatestValue)
                                                            maybeLatestValue.IfSome(latestValue => obs.OnNext(latestValue));
                                                    }
                                                });
                                            }
                                            else
                                            {
                                                maybeLatestValue = value.ToSome();
                                            }
                                        }
                                    },
                                    ex =>
                                    {
                                        lock (syncRoot)
                                        {
                                            isCompleted = true;
                                            obs.OnError(ex);
                                        }
                                    },
                                    () =>
                                    {
                                        lock (syncRoot)
                                        {
                                            isCompleted = true;
                                            obs.OnCompleted();
                                        }
                                    });
                        }));
        }

        public static IObservable<T> DefaultIfEmpty<T>(this IObservable<T> source, IObservable<T> defaultObservable)
        {
            Contract.Requires(source != null);
            Contract.Requires(defaultObservable != null);

            return source.Concat(maybe => !maybe.IsSome ? defaultObservable : Observable.Empty<T>());
        }

        public static IObservable<T> EachUsing<T>(this IObservable<T> source, Func<T, IDisposable> resourceFactory)
        {
            Contract.Requires(source != null);
            Contract.Requires(resourceFactory != null);

            return Observable.Create<T>(obs =>
            {
                IDisposable resource = null;
                var syncRoot = new object();

                var subscription = source.Subscribe(
                    value =>
                    {
                        lock (syncRoot)
                        {
                            resource?.Dispose();
                            resource = resourceFactory(value);
                        }

                        obs.OnNext(value);
                    },
                    ex =>
                    {
                        lock (syncRoot)
                        {
                            resource?.Dispose();
                            resource = null;
                        }

                        obs.OnError(ex);

                    },
                    () =>
                    {
                        lock (syncRoot)
                        {
                            resource?.Dispose();
                            resource = null;
                        }

                        obs.OnCompleted();
                    });

                return StableCompositeDisposable.Create(
                    subscription,
                    Disposable.Create(() =>
                    {
                        lock (syncRoot)
                        {
                            resource?.Dispose();
                            resource = null;
                        }
                    }));
            });
        }

        public static IObservable<T> Forever<T>(T value)
        {
            return Observable.Create<T>(observer =>
            {
                observer.OnNext(value);
                return Disposable.Empty;
            });
        }

        public static IObservable<T> KeepOpen<T>(this IObservable<T> source)
        {
            Contract.Requires(source != null);

            return source.Concat(Observable.Never<T>());
        }

        public static IObservable<T> LazyRefCount<T>(this IConnectableObservable<T> source, TimeSpan delay)
        {
            Contract.Requires(source != null);

            return source.LazyRefCount(delay, Scheduler.Default);
        }

        public static IObservable<T> LazyRefCount<T>(this IConnectableObservable<T> source, TimeSpan delay, IScheduler scheduler)
        {
            Contract.Requires(source != null);

            var syncRoot = new object();
            var serial = new SerialDisposable();
            IDisposable currentConnection = null;

            var innerObservable = ConnectableObservable
                .Create<T>(
                    () =>
                    {
                        lock (syncRoot)
                        {
                            if (currentConnection == null)
                                currentConnection = source.Connect();

                            serial.Disposable = new SingleAssignmentDisposable();
                        }

                        return Disposable
                            .Create(() =>
                            {
                                lock (syncRoot)
                                {
                                    var cancelable = (SingleAssignmentDisposable)serial.Disposable;

                                    cancelable.Disposable = scheduler.Schedule(cancelable, delay, (self, state) =>
                                    {
                                        lock (syncRoot)
                                        {
                                            if (object.ReferenceEquals(serial.Disposable, state))
                                            {
                                                currentConnection.Dispose();
                                                currentConnection = null;
                                            }
                                        }

                                        return Disposable.Empty;
                                    });
                                }
                            });
                    },
                    source.Subscribe)
                .RefCount();

            return Observable.Create<T>(
                outerObserver =>
                {
                    var anonymousObserver = new AnonymousObserver<T>(
                        outerObserver.OnNext,
                        outerObserver.OnError,
                        outerObserver.OnCompleted);

                    return StableCompositeDisposable.Create(
                        anonymousObserver,
                        innerObservable
                            .Subscribe(anonymousObserver));
                });
        }

        public static IObservable<Unit> OnCompletion<T>(this IObservable<T> source)
        {
            Contract.Requires(source != null);

            return Observable.Create<Unit>(observer => source.Subscribe(
                x =>
                {
                },
                observer.OnError,
                () =>
                {
                    observer.OnNext(Unit.Default);
                    observer.OnCompleted();
                }));
        }

        public static IObservable<Exception> OnCompletionOrError<T>(this IObservable<T> source)
        {
            Contract.Requires(source != null);

            return source
                .Materialize()
                .Where(x => !x.HasValue)
                .Select(x => x.Exception);
        }

        public static IObservable<T> Prioritize<T>(this IObservable<T> source, IObservable<T> other)
        {
            Contract.Requires(source != null);

            return source
                .Publish(publishedSource => publishedSource
                    .Merge(other.TakeUntil(publishedSource)));
        }

        public static IObservable<T> RepeatWhileEmpty<T>(this IObservable<T> source)
        {
            Contract.Requires(source != null);

            return source.RepeatWhileEmpty(null);
        }

        public static IObservable<T> RepeatWhileEmpty<T>(this IObservable<T> source, int repeatCount)
        {
            Contract.Requires(source != null);

            return source.RepeatWhileEmpty((int?)repeatCount);
        }

        private static IObservable<T> RepeatWhileEmpty<T>(this IObservable<T> source, int? repeatCount)
        {
            Contract.Requires(source != null);
            Contract.Requires(!repeatCount.HasValue || repeatCount.Value >= 0);

            if ((repeatCount.HasValue) && (repeatCount.Value == 0))
                return Observable.Empty<T>();

            return source
                .Concat(maybe => !maybe.IsSome ? source.RepeatWhileEmpty(repeatCount - 1) : Observable.Empty<T>());
        }

        public static IObservable<TAccumulate> ScanAsync<TSource, TAccumulate>(this IObservable<TSource> source, TAccumulate seed, Func<TAccumulate, TSource, Task<TAccumulate>> accumulator)
        {
            Contract.Requires(source != null);
            Contract.Requires(accumulator != null);

            return source
                .Scan(Task.FromResult(seed), async (currentTask, value) => await accumulator(await currentTask.ConfigureAwait(false), value).ConfigureAwait(false))
                .SelectMany(x => x);
        }

        public static IObservable<Unit> SelectMany<TSource>(this IObservable<TSource> source, Func<TSource, CancellationToken, Task> taskSelector)
        {
            Contract.Requires(source != null);
            Contract.Requires(taskSelector != null);

            return source
                .SelectMany((x, ct) => taskSelector(x, ct).AsUnitTask());
        }

        public static IObservable<TSource> StateScan<TSource, TState>(this IObservable<TSource> source, TState seed, Func<TState, TSource, TState> stateFunction)
        {
            Contract.Requires(source != null);
            Contract.Requires(stateFunction != null);

            return source
                .Scan(
                    Tuple.Create(seed, default(TSource)),
                    (stateTuple, value) => Tuple.Create(stateFunction(stateTuple.Item1, value), value))
                .Select(stateTuple => stateTuple.Item2);
        }

        public static IObservable<T> SubscribeConcurrentlyAtMost<T>(this IObservable<T> source, int count, IObservable<T> continuation)
        {
            Contract.Requires(source != null);
            Contract.Requires(count >= 0);
            Contract.Requires(continuation != null);

            var subscriptionCount = 0;

            return Observable.Create<T>(observer =>
            {
                while (true)
                {
                    var localSubscriptionCount = subscriptionCount;
                    if (localSubscriptionCount >= count)
                        return continuation.Subscribe(observer);

                    if (Interlocked.CompareExchange(ref subscriptionCount, localSubscriptionCount + 1, localSubscriptionCount) == localSubscriptionCount)
                    {
                        var subscription = source.Subscribe(observer);

                        return Disposables.Disposable.Create(() =>
                        {
                            Interlocked.Decrement(ref subscriptionCount);
                            subscription.Dispose();
                        });
                    }
                }
            });
        }
        
        public static IObservable<T> SubscribeTotallyAtMost<T>(this IObservable<T> source, int count, IObservable<T> continuation)
        {
            Contract.Requires(source != null);
            Contract.Requires(count >= 0);
            Contract.Requires(continuation != null);

            var subscriptionCount = 0;

            return Observable.Create<T>(observer =>
            {
                while (true)
                {
                    var localSubscriptionCount = subscriptionCount;
                    if (localSubscriptionCount >= count)
                        return continuation.Subscribe(observer);

                    if (Interlocked.CompareExchange(ref subscriptionCount, localSubscriptionCount + 1, localSubscriptionCount) == localSubscriptionCount)
                        return source.Subscribe(observer);
                }
            });
        }

        public static IObservable<T> TakeUntil<T>(this IObservable<T> source, CancellationToken ct)
        {
            return source.TakeUntil(ct.ToObservable());
        }

        public static IObservable<TSource> TakeWhileInclusive<TSource>(this IObservable<TSource> source, Func<TSource, bool> predicate)
        {
            return source
                .TakeWhileInclusive((x, i) => predicate(x));
        }

        public static IObservable<TSource> TakeWhileInclusive<TSource>(this IObservable<TSource> source, Func<TSource, int, bool> predicate)
        {
            return Observable.Using(
                () => new SingleAssignmentDisposable(),
                disposable => Observable.Create<TSource>(
                    o =>
                    {
                        var index = 0;

                        return disposable.Disposable = source
                            .Subscribe(
                                x =>
                                {
                                    o.OnNext(x);
                                    if (!predicate(x, checked(index++)))
                                        o.OnCompleted();
                                },
                                o.OnError,
                                o.OnCompleted);
                    }));
        }

        public static IObservable<T> ThrowOnCancellation<T>(this IObservable<T> source, CancellationToken ct)
        {
            Contract.Requires(source != null);

            return source.TakeUntil(ct)
                .Concat(Observable.If(
                    () => ct.IsCancellationRequested,
                    Observable.Throw<T>(new OperationCanceledException())));
        }

        public static IObservable<Counting<T>> ToCounting<T>(this IObservable<T> source)
        {
            Contract.Requires(source != null);

            return source.Select((x, i) => new Counting<T>((ulong)i, x));
        }

        #region GroupedObservableImpl
        private sealed class GroupedObservableImpl<TKey, TSource> : IGroupedObservable<TKey, TSource>
        {
            private readonly IObservable<TSource> _baseObservable;

            public GroupedObservableImpl(IObservable<TSource> baseObservable, TKey key)
            {
                Contract.Requires(baseObservable != null);

                this.Key = key;
                this._baseObservable = baseObservable;
            }

            public IDisposable Subscribe(IObserver<TSource> observer)
            {
                return this._baseObservable.Subscribe(observer);
            }

            public TKey Key { get; }
        }
        #endregion

        public static IGroupedObservable<TKey, TSource> ToGroup<TKey, TSource>(this IObservable<TSource> source, TKey key)
        {
            Contract.Requires(source != null);

            return new GroupedObservableImpl<TKey, TSource>(source, key);
        }

        #region NotifyCollectionChangedEventPatternSource
        private sealed class NotifyCollectionChangedEventPatternSource : EventPatternSourceBase<object, NotifyCollectionChangedEventArgs>, INotifyCollectionChanged
        {
            public NotifyCollectionChangedEventPatternSource(IObservable<EventPattern<object, NotifyCollectionChangedEventArgs>> source) : base(source, (invokeAction, eventPattern) => invokeAction(eventPattern.Sender, eventPattern.EventArgs))
            {
                Contract.Requires(source != null);
            }

            public event NotifyCollectionChangedEventHandler CollectionChanged
            {
                add
                {
                    base.Add(value, (o, e) => value(o, e));
                }

                remove
                {
                    base.Remove(value);
                }
            }
        }
        #endregion

        #region NotifyPropertyChangedEventPatternSource
        private sealed class NotifyPropertyChangedEventPatternSource : EventPatternSourceBase<object, PropertyChangedEventArgs>, INotifyPropertyChanged
        {
            public NotifyPropertyChangedEventPatternSource(IObservable<EventPattern<object, PropertyChangedEventArgs>> source) : base(source, (invokeAction, eventPattern) => invokeAction(eventPattern.Sender, eventPattern.EventArgs))
            {
                Contract.Requires(source != null);
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    base.Add(value, (o, e) => value(o, e));
                }

                remove
                {
                    base.Remove(value);
                }
            }
        }
        #endregion

        public static INotifyCollectionChanged ToNotifyCollectionChangedEventPattern(this IObservable<NotifyCollectionChangedEventArgs> source, object sender)
        {
            Contract.Requires(source != null);
            Contract.Ensures(Contract.Result<INotifyCollectionChanged>() != null);

            return new NotifyCollectionChangedEventPatternSource(source.Select(x => new EventPattern<NotifyCollectionChangedEventArgs>(sender, x)));
        }

        public static INotifyPropertyChanged ToNotifyPropertyChangedEventPattern(this IObservable<PropertyChangedEventArgs> source, object sender)
        {
            Contract.Requires(source != null);
            Contract.Ensures(Contract.Result<INotifyPropertyChanged>() != null);

            return new NotifyPropertyChangedEventPatternSource(source.Select(x => new EventPattern<PropertyChangedEventArgs>(sender, x)));
        }

        #region StrongReferenceDisposable
        private sealed class StrongReferenceDisposable : IDisposable
        {
            private readonly IDisposable _innerDisposable;

            // ReSharper disable NotAccessedField.Local
            private object _reference;
            // ReSharper restore NotAccessedField.Local

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public StrongReferenceDisposable(IDisposable innerDisposable, object reference)
            {
                Contract.Requires(innerDisposable != null);

                this._innerDisposable = innerDisposable;
                this._reference = reference;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public void Dispose()
            {
                this._innerDisposable.Dispose();
                this._reference = null;
            }
        }
        #endregion

        #region WeakObserver
        private class WeakObserver<T> : IObserver<T>
        {
            private readonly WeakReference _weakObserverReference;

            public WeakObserver(IObservable<T> observable, IObserver<T> observer)
            {
                Contract.Requires(observable != null);

                this._weakObserverReference = new WeakReference(observer);
                this.BaseSubscription = observable.Subscribe(this);
            }

            public void OnCompleted()
            {
                var observer = this._weakObserverReference.Target as IObserver<T>;

                if (observer != null)
                    observer.OnCompleted();
                else
                    this.BaseSubscription.Dispose();
            }

            public void OnError(Exception error)
            {
                var observer = this._weakObserverReference.Target as IObserver<T>;

                if (observer != null)
                    observer.OnError(error);
                else
                    this.BaseSubscription.Dispose();
            }

            public void OnNext(T value)
            {
                var observer = this._weakObserverReference.Target as IObserver<T>;

                if (observer != null)
                    observer.OnNext(value);
                else
                    this.BaseSubscription.Dispose();
            }

            public IDisposable BaseSubscription { get; }
        }
        #endregion

        public static IObservable<T> ToWeakObservable<T>(this IObservable<T> observable)
        {
            return Observable.Create<T>(baseObserver =>
            {
                var weakObserver = new WeakObserver<T>(observable, baseObserver);

                return new StrongReferenceDisposable(weakObserver.BaseSubscription, baseObserver);
            });
        }

        public static IObservable<Option<T>> TryFirstAsync<T>(this IObservable<T> source)
        {
            Contract.Requires(source != null);

            return source
                .Select(x => (Option<T>)x)
                .FirstOrDefaultAsync();
        }

        public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, CancellationToken, Task<bool>> predicate)
        {
            Contract.Requires(source != null);
            Contract.Requires(predicate != null);

            return source
                .SelectMany(x => ObservableExtensions
                    .WithCancellation(
                        ct => predicate(x, ct)
                            .ToObservable()
                            .Where(b => b)
                            .Select(b => x)));
        }

        public static IObservable<T> WhereNotNull<T>(this IObservable<T> source) where T : class
        {
            Contract.Requires(source != null);

            return source.Where(t => !object.Equals(t, default(T)));
        }

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