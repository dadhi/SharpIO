/*
Modified from the original https://gist.github.com/louthy/524fbe8965d3a2aae1b576cdd8e971e4

- removed dependency on [language-ext](https://github.com/louthy/language-ext)
- separated monadic boilerplate, so you may concentrate on describing the operations and interpretation of the program
- removed `IO<A>.Faulted` to simplify the examples. It can be added back in straightforward manner.
 
Useful links:
- [John DeGoes: Beyond Free Monads - λC Winter Retreat 2017](https://www.youtube.com/watch?v=A-lmrvsUi2Y)
- [Free and tagless compared - how not to commit to a monad too early](https://softwaremill.com/free-tagless-compared-how-not-to-commit-to-monad-too-early)

Requires C# 7.2
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FreeIOMonadExample
{
    using static Unit;
    using static NumberLinesOperations;

    static class Program
    {
        public static async Task Main()
        {
            // Compose, e.g. describe program without running it
            var program = PrefixLines("d:/some_text_file.txt");

            // Run program by interpreting its operations
            MockRunner.Run(program);
            MockRunner.Run(program, skipLogging: true);
            LiveRunner.Run(program);
            await LiveRunnerAsync.Run(program);
        }

        // Program description
        private static IO<Unit> PrefixLines(string path) =>
              from lines in ReadAllLines(path)
              from _1 in Log($"There are {lines.Count()} lines")
              from _2 in Log("Prepending line numbers")
              let newLines = Enumerable.Range(1, int.MaxValue).Zip(lines, (i, line) => $"{i}: {line}")
              let newFile = path + ".prefixed"
              from _3 in WriteAllLines(newFile, newLines)
              from _4 in Log($"Lines prepended and file saved successfully to \"{newFile}\"")
              select unit;
    }

    public readonly struct WriteAllLines
    {
        public readonly string Path;
        public readonly IEnumerable<string> Lines;
        public WriteAllLines(string path, IEnumerable<string> lines) => (Path, Lines) = (path, lines);
    }

    public readonly struct ReadAllLines
    {
        public readonly string Path;
        public ReadAllLines(string path) => Path = path;
    }

    public readonly struct Log
    {
        public readonly string Message;
        public Log(string message) => Message = message;
    }

    public static class NumberLinesOperations // program algebra
    {
        public static IO<Unit> WriteAllLines(string path, IEnumerable<string> lines) =>
            new WriteAllLines(path, lines).ToIO();

        public static IO<IEnumerable<string>> ReadAllLines(string path) =>
            new ReadAllLines(path).ToIO<ReadAllLines, IEnumerable<string>>();

        public static IO<Unit> Log(string message) =>
            new Log(message).ToIO();
    }

    public static class LiveRunner
    {
        public static A Run<A>(IO<A> m) =>
            m is Return<A> r ? r.Result
            : m is IO<ReadAllLines, IEnumerable<string>, A> ra ? Run(ra.Next(File.ReadAllLines(ra.Input.Path)))
            : m is IO<WriteAllLines, Unit, A> wa ? Run(wa.Next(fun(() => File.WriteAllLines(wa.Input.Path, wa.Input.Lines))))
            : m is IO<Log, Unit, A> log ? Run(log.Next(fun(() => Console.WriteLine(log.Input.Message))))
            : throw new NotSupportedException($"Not supported operation {m}");
    }

    public static class LiveRunnerAsync
    {
        public static async Task<A> Run<A>(IO<A> p) =>
            p is Return<A> r ? r.Result
            : p is IO<ReadAllLines, IEnumerable<string>, A> ra ? await Run(ra.Next(await ReadAllLines(ra.Input.Path)))
            : p is IO<WriteAllLines, Unit, A> wa ? await Run(wa.Next(await WriteAllLines(wa.Input.Path, wa.Input.Lines)))
            : p is IO<Log, Unit, A> log ? await Run(log.Next(fun(() => Console.WriteLine(log.Input.Message))))
            : throw new NotSupportedException($"Not supported operation {p}");

        static Task<Unit> WriteAllLines(string path, IEnumerable<string> output) =>
            Task.Run(() => fun(() => File.WriteAllLines(path, output)));

        static Task<IEnumerable<string>> ReadAllLines(string path) =>
            Task.Run<IEnumerable<string>>(() => File.ReadAllLines(path));
    }

    public static class MockRunner
    {
        // Example of non-recursive (stack-safe) interpreter
        public static A Run<A>(IO<A> m, bool skipLogging = false)
        {
            while (true)
                switch (m)
                {
                    case Return<A> x:
                        return x.Result;
                    case IO<ReadAllLines, IEnumerable<string>, A> x:
                        m = x.Next(MockReadAllLines(x.Input.Path));
                        break;
                    case IO<WriteAllLines, Unit, A> x:
                        m = x.Next(unit); // do nothing, not interested in output
                        break;
                    case IO<Log, Unit, A> log:
                        m = skipLogging ? log.Next(unit) : log.Next(fun(() => Console.WriteLine(log.Input.Message)));
                        break;
                    default: throw new NotSupportedException($"Not supported operation {m}");
                }
        }

        static IEnumerable<string> MockReadAllLines(string path) =>
            new[] { "Hello", "World", path };
    }

    // Monadic IO implementation, can be reused, published to NuGet, etc.
    //-------------------------------------------------------------------

    public interface IO<A>
    {
        IO<B> Bind<B>(Func<A, IO<B>> f);
    }

    public sealed class Return<A> : IO<A>
    {
        public readonly A Result;
        public Return(A a) => Result = a;

        public IO<B> Bind<B>(Func<A, IO<B>> f) => f(Result);
    }

    public class IO<I, R, A> : IO<A>
    {
        public readonly I Input;
        public readonly Func<R, IO<A>> Next;
        public IO(I input, Func<R, IO<A>> next) => (Input, Next) = (input, next);

        public IO<B> Bind<B>(Func<A, IO<B>> f) => new IO<I, R, B>(Input, r => Next(r).Bind(f));
    }

    public static class IOMonad
    {
        public static IO<A> Wrap<A>(this A a) =>
            new Return<A>(a);

        public static IO<B> Select<A, B>(this IO<A> m, Func<A, B> f) =>
            m.Bind(a => f(a).Wrap());

        public static IO<C> SelectMany<A, B, C>(this IO<A> m, Func<A, IO<B>> f, Func<A, B, C> project) =>
            m.Bind(a => f(a).Bind(b => project(a, b).Wrap()));
    }

    public static class IOMonadSugar
    {
        public static IO<Unit> ToIO<O>(this O op) => new IO<O, Unit, Unit>(op, IOMonad.Wrap);
        public static IO<R> ToIO<O, R>(this O op) => new IO<O, R, R>(op, IOMonad.Wrap);
    }

    public sealed class Unit
    {
        public static readonly Unit unit = new Unit();
        public static Unit fun(Action a) { a(); return unit; }

        private Unit() { }
    }
}