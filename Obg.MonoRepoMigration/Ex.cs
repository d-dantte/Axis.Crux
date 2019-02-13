using Microsoft.Build.Construction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Obg.MonoRepoMigration
{
    public static class Ext
    {
        public static DirectoryInfo ChildDirectory(this DirectoryInfo parent, string childDirectoryName)
        {
            return new DirectoryInfo(Path.Combine(parent.FullName, childDirectoryName));
        }

        public static bool ContainsProjectInfo(XElement propertyGroup)
        {
            if (propertyGroup == null)
                return false;

            else if (propertyGroup.Name.LocalName != "PropertyGroup")
                return false;

            else if (propertyGroup.Find("TargetFramework") == null)
                return false;

            else return true;
        }

        public static Func<XElement, bool> HasAttribute(string name, string value)
        {
            return xelt => xelt.Attribute(name).Value == value;
        }

        public static bool TryGet<V>(this IEnumerable<V> values, out V result, Func<V, bool> predicate)
        {
            try
            {
                result = values.First(predicate);
                return true;
            }
            catch
            {
                result = default(V);
                return false;
            }
        }

        public static string JoinUsing(this IEnumerable<string> strings, string separator) => string.Join(separator, strings);

        public static string ResolveNuspecPath(this FileInfo csproj)
        {
            return $"{csproj.DirectoryName}/{csproj.Name.TrimEnd("csproj")}nuspec";
        }

        public static bool IsMonoRepoFormat(this XDocument csproj)
        {
            return csproj?.Root
                .Attributes()
                .Any(IsSdkAttribute) == true;
        }

        public static bool IsSdkAttribute(this XAttribute att) => att?.Name.LocalName == "Sdk";

        public static R Using<D, R>(this D disposable, Func<D, R> func)
        where D : IDisposable
        {
            using (disposable)
            {
                return func(disposable);
            }
        }
        public static void Using<D>(this D disposable, Action<D> func)
        where D : IDisposable
        {
            using (disposable)
            {
                func(disposable);
            }
        }

        public static void ForAll<T>(this IEnumerable<T> enm, Action<T> action)
        {
            foreach (var t in enm)
                action(t);
        }

        public static string TrimEnd(this string @string, string match)
        {
            if (@string?.EndsWith(match) == true)
                return @string.Substring(0, @string.Length - match.Length);

            return @string;
        }

        public static XElement Find(this XElement parent, string path)
        {
            var ns = parent.Name.Namespace;
            if (string.IsNullOrWhiteSpace(path))
                return null;
            else
                return path
                    .Split('/')
                    .Aggregate(parent, (elt, segment) => elt?.Element(ns + segment));
        }

        public static IEnumerable<XElement> FindAll(this XElement parent, params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return new XElement[0];

            var ns = parent.Name.Namespace;
            //breath-first traversal of the nodes
            return paths
                .SelectMany(path =>
                {
                    var parents = new[] { parent }.AsEnumerable();
                    return path.Split('/')
                        .Aggregate(parents, (prnts, segment) =>
                        {
                            return prnts.SelectMany(prnt =>
                            {
                                return prnt.Elements(ns + segment);
                            });
                        })
                        .ToArray();
                })
                .ToArray();
        }

        public static bool IsNotNull(this object obj) => obj != null;

        public static bool HasAttribute(this XElement element, string name)
        {
            return element.HasAttribute(name, out XAttribute x);
        }
        public static bool HasAttribute(this XElement element, string name, out XAttribute attribute)
        {
            attribute = element.Attribute(name);
            return attribute != null;
        }

        public static bool HasChild(this XElement element, string name, out XElement child)
        {
            var ns = element.Name.Namespace;
            child = element.Element(ns + name);
            return child != null;
        }

        public static bool ContainsAll(this string @string, params string[] substrings)
        {
            return substrings.All(@string.Contains);
        }

        public static V GetOrAdd<K, V>(this Dictionary<K, V> dict, K key, Func<K, V> generator)
        {
            if (dict.ContainsKey(key)) return dict[key];
            else
            {
                var value = generator.Invoke(key);
                dict.Add(key, value);

                return value;
            }
        }

        public static string ResolveVerticalName(this FileInfo csprojFile)
        => csprojFile.Directory.Parent.ResolveVerticalName();

        public static string ResolveVerticalName(this DirectoryInfo verticalDirectory)
        => verticalDirectory
            .GetFiles("*.sln")
            .FirstOrDefault()
            ?.Name.TrimEnd(".sln");

        public static string ExtractProjectNameFromFilePath(this string filePath) 
        => Path.GetFileName(filePath).TrimEnd(".csproj");
    }
}
