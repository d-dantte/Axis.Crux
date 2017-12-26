using System;

namespace Axis.Crux.VSpec
{
    public static class Extensions
    {

        public static R Using<R, D>(this D disposable, Func<D, R> func)
        where D : IDisposable
        {
            using (disposable)
                return func(disposable);
        }
        public static void Using<D>(this D disposable, Action<D> action)
        where D : IDisposable
        {
            using (disposable)
                action(disposable);
        }

        public static R Pipe<V, R>(this V value, Func<V, R> pipe)
        {
            return pipe.Invoke(value);
        }

        public static void Pipe<V>(this V value, Action<V> pipe)
        {
            pipe.Invoke(value);
        }
    }
}
