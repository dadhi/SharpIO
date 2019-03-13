namespace SharpIODemo
{
    using System;
    using SharpIOLib;
    using static SharpIOLib.Empty;
    using static SharpIOLib.SharpIO;

    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("🌄");
        }

        public static IO<R, Exception, Empty> GetProgram<R>()
            where R : ILog<R>, IConsole<R> =>
            from logStart in Log.Info<R>("Starting the quiz...")
            from askForName in ConsoleX.WriteLine<R>("Enter your name:")
            from name in ConsoleX.ReadLine<R>()
            //from logName in Log.Info<R>($"Name is {name}")
            select empty;
    }

    public interface IConsole<in _>
    {
        IO<_, CannotFail, Empty> WriteLine(string line);
        IO<_, Exception, string> ReadLine();
    }

    public static class ConsoleX
    {
        public static IO<R, CannotFail, Empty> WriteLine<R>(string line)
            where R : IConsole<R> =>
            Runtime<R, CannotFail>().Bind(r => r.WriteLine(line));

        public static IO<R, Exception, string> ReadLine<R>()
            where R : IConsole<R> =>
            Runtime<R, Exception>().Bind(r => r.ReadLine());
    }

    public interface ILog<in _>
    {
        IO<_, CannotFail, Empty> Info(string message);
    }

    public static class Log
    {
        public static IO<R, CannotFail, Empty> Info<R>(string message)
            where R : ILog<R> =>
            Runtime<R, CannotFail>().Bind(c => c.Info(message));
    }

    public class TestConsole<_> : IConsole<_>
    {
        public IO<_, CannotFail, Empty> WriteLine(
            string line) => empty.ToIO<_, CannotFail>();
        public IO<_, Exception, string> ReadLine() =>
            "hey".ToIO<_, Exception, string>();
    }

    public class TestLog<_> : ILog<_>
    {
        public IO<_, CannotFail, Empty> Info(string message) => empty.ToIO<_, CannotFail>();
    }
}

namespace SharpIOLib
{
    using System;
    using static Empty;

    public abstract class CannotFail
    {
        private CannotFail() { }
    }

    public class Empty
    {
        public static readonly Empty empty = new Empty();
    }

    public interface IO<in R, out E, out A>
    {
        IResult<E, A> Run(R runtime);
    }

    public struct SIO<R, E, A> : IO<R, E, A>
    {
        public Func<R, IResult<E, A>> Runner;
        public SIO(Func<R, IResult<E, A>> runner) => Runner = runner;
        public IResult<E, A> Run(R runtime) => Runner(runtime);
    }

    public interface IResult<out E, out A> { }

    public struct Success<E, A> : IResult<E, A>
    {
        public readonly A Value;
        public Success(A a) => Value = a;
    }

    public struct Failure<E, A> : IResult<E, A>
    {
        public readonly E Error;
        public Failure(E e) => Error = e;
        public Failure<E, B> To<B>() => new Failure<E, B>(Error);
    }

    public static class SharpIO
    {
        public static IResult<E, A> Success<E, A>(
            A a, E _ = default) => new Success<E, A>(a);

        public static Success<CannotFail, A> Success<A>(
            A a) => new Success<CannotFail, A>(a);

        public static IResult<E, A> Failure<E, A>(
            E e, A _ = default) => new Failure<E, A>(e);

        public static IResult<E, A> RunIO<R, E, A>(
            this IResult<E, A> a, R runtime = default) => a;

        public static IO<R, E, A> ToIO<R, E, A>(
            this A a, E _ = default, R __ = default) =>
            new SIO<R, E, A>(Success(a, _).RunIO);

        public static IO<R, E, Empty> ToIO<R, E>(
            this Empty none, R runtime = default) =>
            new SIO<R, E, Empty>(Success(empty, default(E)).RunIO);

        public static IO<R, E, R> Runtime<R, E>(E _ = default) =>
            new SIO<R, E, R>(r => Success(r, default(E)));

        public static IResult<E, B> To<E, A, B>(
            this IResult<E, A> a, Func<A, B> map) =>
            a is Success<E, A> suc
            ? Success<E, B>(map(suc.Value))
            : ((Failure<E, A>)a).To<B>();

        public static IO<R, E, B> To<R, E, A, B>(
            this IO<R, E, A> a, Func<A, B> map) =>
            new SIO<R, E, B>(r => a.Run(r).To(map));

        public static IO<R, E, B> Bind<R, E, A, B>(
            this IO<R, E, A> a, Func<A, IO<R, E, B>> map) =>
            new SIO<R, E, B>(r =>
            {
                var x = a.Run(r);
                return x is Success<E, A> suc
                    ? map(suc.Value).Run(r)
                    : ((Failure<E, A>)x).To<B>().RunIO(r);
            });

        public static IO<R, E, B> BindCannotFail<R, E, A, B>(
          this IO<R, CannotFail, A> a, Func<A, IO<R, E, B>> map) =>
          new SIO<R, E, B>(r => map(((Success<CannotFail, A>)a.Run(r)).Value).Run(r));

        public static IO<R, E, B> BindToCannotFail<R, E, A, B>(
            this IO<R, CannotFail, A> a, Func<A, IO<R, E, B>> map) =>
            new SIO<R, E, B>(r => map(((Success<CannotFail, A>)a.Run(r)).Value).Run(r));

        public static IO<R, E, B> Select<R, E, A, B>(this IO<R, E, A> a, Func<A, B> map) =>
            a.To(map);

        public static IO<R, E, C> SelectMany<R, E, A, B, C>(this IO<R, E, A> a, Func<A, IO<R, E, B>> map, Func<A, B, C> project) =>
            a.Bind(x => map(x).Bind(y => project(x, y).ToIO<R, E, C>()));

        public static IO<R, E, C> SelectMany<R, E, A, B, C>(this IO<R, CannotFail, A> a, Func<A, IO<R, E, B>> map, Func<A, B, C> project) =>
            a.BindCannotFail(x => map(x).Bind(y => project(x, y).ToIO<R, E, C>()));
    }
}
