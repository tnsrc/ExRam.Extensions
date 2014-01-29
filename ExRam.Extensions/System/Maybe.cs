using System;
using System.Diagnostics.Contracts;

namespace System
{
    public struct Maybe<T>
    {
        public static readonly Maybe<T> Null = new Maybe<T>();

        private readonly T _value;
        private readonly bool _hasValue;

        public Maybe(T value)
        {
            this._hasValue = true;
            this._value = value;
        }

        public static implicit operator Maybe<T>(T value)
        {
            return new Maybe<T>(value);
        }

        public static explicit operator T(Maybe<T> maybe)
        {
            return maybe.Value;
        }

        public static bool operator ==(Maybe<T> counting1, Maybe<T> counting2)
        {
            return counting1.Equals(counting2);
        }

        public static bool operator !=(Maybe<T> counting1, Maybe<T> counting2)
        {
            return !(counting1 == counting2);
        }

        public Maybe<TResult> Cast<TResult>()
        {
            if (this._hasValue)
            {
                var boxed = (object)this._value;
                return ((boxed != null) ? (new Maybe<TResult>((TResult)boxed)) : (new Maybe<TResult>(default(TResult))));
            }

            return Maybe<TResult>.Null;
        }

        public T GetValueOrDefault()
        {
            return this._value;
        }

        public override bool Equals(object obj)
        {
            if (obj is Maybe<T>)
            {
                var maybe2 = (Maybe<T>)obj;

                if (this._hasValue)
                {
                    if (maybe2.HasValue)
                        return object.Equals(this._value, maybe2._value);
                }
                else
                    return (!maybe2._hasValue);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return ((this._hasValue) ? (this._value.GetHashCode()) : (0));
        }

        public T Value
        {
            get
            {
                if (!this._hasValue)
                    throw new InvalidOperationException();

                return this._value;
            }
        }

        public bool HasValue
        {
            get
            {
                return this._hasValue;
            }
        }
    }
}