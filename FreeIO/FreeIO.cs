/*
Modified from the original https://gist.github.com/louthy/524fbe8965d3a2aae1b576cdd8e971e4

- removed dependency on [language-ext](https://github.com/louthy/language-ext)
- separated monadic boilerplate, so you may concentrate on describing the operations and interpretation of the program
- removed `IO<A>.Faulted` to simplify the examples. It can be added back in straightforward manner.
 
Useful links:
- [John DeGoes: Beyond Free Monads - λC Winter Retreat 2017](https://www.youtube.com/watch?v=A-lmrvsUi2Y)
- [Free and Tagless compared - how not to commit to a monad too early](https://softwaremill.com/free-tagless-compared-how-not-to-commit-to-monad-too-early)
- [John A De Goes - ZIO: Next-Generation Effects in Scala 2019](https://www.youtube.com/watch?v=mkSHhsJXjdc)

Requires: 

For `LiveRunner` to work you need "d:/some_text_file.txt" with couple of lines of text

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static FreeIO.Unit;
using SharpIO;

namespace FreeIO.Example
{
    using static NumberLinesOperations;

    public class Program
    {
        public static async Task Main()
        {
            // Describe program without running it:
            var program = NumberLines(@"C:\Dev\some_text_file.txt");

            // Actually running the program using different runners (interpreters):
            TestRunner.Run(program);
            TestRunner.Run(program, skipLogging: true);
            LiveRunner.Run(program);
            await AsyncLiveRunner.RunAsync(program);
        }

        // Program description
        private static IO<Unit> NumberLines(string path) =>
              from lines in ReadAllLines(path)
              from _1 in Log($"There are {lines.Count()} lines")
              from _2 in Log("Pre-pending the line numbers")
              let newLines = Enumerable.Range(1, int.MaxValue).Zip(lines, (i, line) => $"{i}: {line}")
              let newFile = path + ".prefixed"
              from _3 in WriteAllLines(newFile, newLines)
              from _4 in Log($"Lines prepended and file saved successfully to '{newFile}'")
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
        public static A Run<A>(IO<A> program) => program switch
        {
            IO<ReadAllLines, IEnumerable<string>, A> p => Run(
                p.Get(File.ReadAllLines(p.Input.Path))),
            
            IO<WriteAllLines, Unit, A> p => Run(
                p.Do(x => File.WriteAllLines(x.Path, x.Lines))),

            IO<Log, Unit, A> p => Run(
                p.Do(x => Console.WriteLine(x.Message))),

            _ => program.Result()
        };
    }

    public static class AsyncLiveRunner
    {
        public static async Task<A> RunAsync<A>(IO<A> program) => program switch
        {
            IO<ReadAllLines, IEnumerable<string>, A> p => await RunAsync(
                p.Get(await File.ReadAllLinesAsync(p.Input.Path))),
            
            IO<WriteAllLines, Unit, A> p => await RunAsync(
                p.Do(async x => await File.WriteAllLinesAsync(x.Path, x.Lines))),
            
            IO<Log, Unit, A> p => await RunAsync(
                p.Do(i => Console.WriteLine(i.Message))),
            
            _ => program.Result()
        };
    }

    public static class TestRunner
    {
        // Example of non-recursive (stack-safe) interpreter
        public static A Run<A>(IO<A> program, bool skipLogging = false)
        {
            IEnumerable<string> ReadAllLines(string path) => new[] { "Hello", "World", path };

            while (program is IComplete == false)
                program = program switch
                {
                    IO<ReadAllLines, IEnumerable<string>, A> p => p.Get(ReadAllLines(p.Input.Path)),
                    
                    IO<WriteAllLines, Unit, A> p => p.Skip(),
                    
                    IO<Log, Unit, A> p => (skipLogging ? p.Skip() : p.Do(x => Console.WriteLine(x.Message))),
                    
                    _ => program // todo should we throw?
                };

            return program.Result();
        }
    }
}

namespace FreeIO2
{
    public interface IComplete { }

    public interface IO2<out T, TErr, TVal> where T : IResult<TErr, TVal>
    {
        IO2<R, RErr, RVal> Bind<R, RErr, RVal>(Func<T, IO2<R, RErr, RVal>> f) where R : IResult<RErr, RVal>;
    }

    public class IO2<TIn, TOut, T, TErr, TVal> : IO2<T, TErr, TVal> where T : IResult<TErr, TVal>
    {
        public readonly TIn Input;
        public readonly Func<TOut, IO2<T, TErr, TVal>> Wrap;

        public IO2(TIn input, Func<TOut, IO2<T, TErr, TVal>> wrap) => (Input, Wrap) = (input, wrap);

        public IO2<R, RErr, RVal> Bind<R, RErr, RVal>(Func<T, IO2<R, RErr, RVal>> f) where R : IResult<RErr, RVal> =>
            new IO2<TIn, TOut, R, RErr, RVal>(Input, x => Wrap(x).Bind(f));
    }

    public struct Complete2<T, TErr, TVal> : IO2<T, TErr, TVal>, IComplete where T : IResult<TErr, TVal>
    {
        public readonly T Result;

        public Complete2(T result) => Result = result;

        public IO2<R, RErr, RVal> Bind<R, RErr, RVal>(Func<T, IO2<R, RErr, RVal>> f) where R : IResult<RErr, RVal> =>
            f(Result);
    }

    public static class IOLinq
    {
        public static IO2<T, TErr, TVal> Pure<T, TErr, TVal>(this T result) 
            where T : IResult<TErr, TVal> =>
            new Complete2<T, TErr, TVal>(result);

        public static IO2<R, RErr, RVal> Select<T, TErr, TVal, R, RErr, RVal>(this IO2<T, TErr, TVal> m, Func<T, R> f) 
            where T : IResult<TErr, TVal>
            where R : IResult<RErr, RVal> =>
            m.Bind(a => f(a).Pure<R, RErr, RVal>());

        public static IO2<R, RErr, RVal> SelectMany<T, TErr, TVal, R, RErr, RVal, M, MErr, MVal>(this IO2<T, TErr, TVal> m, Func<T, IO2<M, MErr, MVal>> f, Func<T, M, R> project)
            where T : IResult<TErr, TVal>
            where M : IResult<MErr, MVal>
            where R : IResult<RErr, RVal> =>
            m.Bind(a => f(a).Bind(b => project(a, b).Pure<R, RErr, RVal>()));

        public static T Result<T, TErr, TVal>(this IO2<T, TErr, TVal> m)
            where T : IResult<TErr, TVal> =>
            ((Complete2<T, TErr, TVal>)m).Result;
    }
}

// The actual library implementation 
namespace FreeIO
{ 
    public interface IO<out A>
    {
        IO<B> Bind<B>(Func<A, IO<B>> f);
    }

    public interface IComplete { }

    public struct Complete<A> : IO<A>, IComplete
    {
        public static readonly IComplete NoResult = new Complete<Unit>();

        public readonly A Result;

        public Complete(A a) => Result = a;

        public IO<B> Bind<B>(Func<A, IO<B>> f) => f(Result);
    }

    public class IO<I, O, A> : IO<A>
    {
        public readonly I Input;
        public readonly Func<O, IO<A>> Get;

        public IO(I input, Func<O, IO<A>> next) => (Input, Get) = (input, next);

        public IO<B> Bind<B>(Func<A, IO<B>> f) => new IO<I, O, B>(Input, r => Get(r).Bind(f));
    }

    public static class IOMonad
    {
        public static IO<A> Pure<A>(this A a) =>
            new Complete<A>(a);

        public static IO<B> Select<A, B>(this IO<A> m, Func<A, B> f) =>
            m.Bind(a => f(a).Pure());

        public static IO<C> SelectMany<A, B, C>(this IO<A> m, Func<A, IO<B>> f, Func<A, B, C> project) =>
            m.Bind(a => f(a).Bind(b => project(a, b).Pure()));

        public static A Result<A>(this IO<A> m) =>
            ((Complete<A>)m).Result;
    }

    public static class IOMonadSugar
    {
        public static IO<R> ToIO<I, R>(this I input) => new IO<I, R, R>(input, IOMonad.Pure);

        public static IO<Unit> ToIO<I>(this I input) => input.ToIO<I, Unit>();

        public static IO<A> Skip<I, A>(this IO<I, Unit, A> io) => io.Get(unit);

        public static IO<A> Do<I, A>(this IO<I, Unit, A> io, Action<I> effect)
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
