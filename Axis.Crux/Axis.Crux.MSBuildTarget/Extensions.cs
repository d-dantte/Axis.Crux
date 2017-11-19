using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace Axis.Crux.MSBuildTarget
{
    public static class Extensions
    {
        public static IEnumerable<Branch> Remotes(this IEnumerable<Branch> branches) => branches.Where(_b => _b.IsRemote);
        public static IEnumerable<Branch> Locals(this IEnumerable<Branch> branches) => branches.Where(_b => !_b.IsRemote);

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

        public static IEnumerable<string> ReadLines(this StreamReader reader)
        {
            var lines = new List<string>();
            string line = null;
            while ((line = reader.ReadLine()) != null)
                lines.Add(line);

            return lines;
        }

        public static string JoinUsing(this IEnumerable<string> sequence, string separator) => string.Join(separator, sequence);

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
