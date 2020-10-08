/*
Modified from the original https://gist.github.com/louthy/524fbe8965d3a2aae1b576cdd8e971e4

- removed dependency on [language-ext](https://github.com/louthy/language-ext)
- separated monadic boilerplate, so you may concentrate on describing the operations and interpretation of the program
- removed `IO<A>.Faulted` to simplify the examples. It can be added back in straightforward manner.
 
Useful links:
- [John DeGoes: Beyond Free Monads - λC Winter Retreat 2017](https://www.youtube.com/watch?v=A-lmrvsUi2Y)
- [Free and Tagless compared - how not to commit to a monad too early](https://softwaremill.com/free-tagless-compared-how-not-to-commit-to-monad-too-early)
- [John A De Goes - ZIO: Next-Generation Effects in Scala 2019](https://www.youtube.com/watch?v=mkSHhsJXjdc)

Requires C# 7.2
For `LiveRunner` to work you need "d:/some_text_file.txt" with couple of lines of text
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FreeIO
{
    using static Unit;
    using static NumberLinesOperations;

    public class Program
    {
        public static async Task Main()
        {
            // Describe program without running it:
            var program = NumberLines(@"C:\Dev\SharpIO\some_text_file.txt");

            // Actually running the program using different runners (interpreters):
            Console.WriteLine("- TestRunner:");
            TestRunner.Run(program);

            Console.WriteLine("- TestRunner with no logging - empty runner:");
            TestRunner.Run(program, skipLogging: true);
            
            Console.WriteLine("- LiveRunner:");
            LiveRunner.Run(program);

            Console.WriteLine("- AsyncLiveRunner:");
            await AsyncLiveRunner.Run(program);
        }

        // Program description
        private static IO<Unit> NumberLines(string path) =>
              from lines in ReadAllLines(path)
              from logLineCount in Log($"There are {lines.Count()} lines")
              from logHeader in Log("Pre-pending the line numbers")
              let newLines = Enumerable.Range(1, int.MaxValue).Zip(lines, (i, line) => $"{i}: {line}")
              let newFile = path + ".prefixed"
              from flushLines in WriteAllLines(newFile, newLines)
              from logSuccess in Log($"Lines prepended and file saved successfully to '{newFile}'")
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

    public static class NumberLinesOperations
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
        public static A Run<A>(IO<A> program)
        {
            switch (program)
            {
                case P<ReadAllLines, IEnumerable<string>, A> p:
                    // return Run(p.Step(File.ReadAllLines(p.Input.Path)));
                    return Run(p.Step(TestRunner.ReadAllLines(p.Input.Path)));

                case P<WriteAllLines, Unit, A> p: 
                    // return Run(p.Do(x => File.WriteAllLines(x.Path, x.Lines)));
                    return Run(p.Do(x => TestRunner.WriteAllLines(x.Path, x.Lines)));

                case P<Log, Unit, A> p: 
                    return Run(p.Do(log => Console.WriteLine(log.Message)));

                default:
                    return program.Return();
            }
        }
    }

    public static class AsyncLiveRunner
    {
        public static async Task<A> Run<A>(IO<A> program)
        {
            switch (program)
            {
                case P<ReadAllLines, IEnumerable<string>, A> p:
                    // return await Run(p.Step(await File.ReadAllLinesAsync(p.Input.Path)));
                    return await Run(p.Step(await TestRunner.ReadAllLinesAsync(p.Input.Path)));

                case P<WriteAllLines, Unit, A> p:
                    // return await Run(p.Do(async x => await File.WriteAllLinesAsync(x.Path, x.Lines)));
                    return await Run(p.Do(async x => await TestRunner.WriteAllLinesAsync(x.Path, x.Lines)));

                case P<Log, Unit, A> p:
                    return await Run(p.Do(log => Console.WriteLine(log.Message)));

                default:
                    return program.Return();
            }
        }
    }

    public static class TestRunner
    {
        public static IEnumerable<string> ReadAllLines(string path) => new[] { "Hello", "World", path };
        public static void WriteAllLines(string path, IEnumerable<string> content) {}
        public static Task<IEnumerable<string>> ReadAllLinesAsync(string path) => Task.FromResult(ReadAllLines(path));
        public static Task WriteAllLinesAsync(string path, IEnumerable<string> content) => Task.CompletedTask;

        // Example of non-recursive (stack-safe) runner
        public static A Run<A>(IO<A> program, bool skipLogging = false)
        {
            while (true)
                switch (program)
                {
                    case P<ReadAllLines, IEnumerable<string>, A> p:
                        program = p.Step(ReadAllLines(p.Input.Path));
                        break;
                    case P<WriteAllLines, Unit, A> p:
                        program = p.Skip();
                        break;
                    case P<Log, Unit, A> p:
                        program = skipLogging ? p.Skip() : p.Do(x => Console.WriteLine(x.Message));
                        break;
                    default:
                        return program.Return();
                }
        }
    }

    // Monadic IO implementation, can be reused, published to NuGet, etc.
    //-------------------------------------------------------------------

    public interface IO<out A>
    {
        IO<B> Bind<B>(Func<A, IO<B>> f);
    }

    public struct Return<A> : IO<A>
    {
        public readonly A Result;
        public Return(A a) => Result = a;
        public IO<B> Bind<B>(Func<A, IO<B>> f) => f(Result);
    }


    public class P<I, O, A> : IO<A>
    {
        public readonly I Input;
        public readonly Func<O, IO<A>> Step;

        public P(I input, Func<O, IO<A>> step) => (Input, Step) = (input, step);

        public IO<B> Bind<B>(Func<A, IO<B>> f) => new P<I, O, B>(Input, o => Step(o).Bind(f));
    }

    public static class IOMonad
    {
        public static Func<O, IO<B>> Free<O, A, B>(Func<O, IO<A>> io, Func<A, IO<B>> f) => 
            o => io(o).Bind(f);

        public static IO<A> Return<A>(this A a) =>
            new Return<A>(a);

        public static IO<B> Select<A, B>(this IO<A> m, Func<A, B> f) =>
            m.Bind(a => f(a).Return());

        public static IO<C> SelectMany<A, B, C>(this IO<A> m, Func<A, IO<B>> f, Func<A, B, C> project) =>
            m.Bind(a => f(a).Bind(b => project(a, b).Return()));

        public static A Return<A>(this IO<A> m) =>
            ((Return<A>)m).Result;
    }

    public static class IOMonadSugar
    {
        public static IO<R> ToIO<I, R>(this I input) => new P<I, R, R>(input, IOMonad.Return);

        public static IO<Unit> ToIO<I>(this I input) => input.ToIO<I, Unit>();

        public static IO<A> Skip<I, A>(this P<I, Unit, A> io) => io.Step(unit);

        public static IO<A> Do<I, A>(this P<I, Unit, A> io, Action<I> effect)
        {
            effect(io.Input);
            return io.Skip();
        }
    }

    public struct Unit
    {
        public static readonly Unit unit = new Unit();
    }
}
