namespace SharpIODemo
{
    using System;
    using SharpIOLib;
    using static SharpIOLib.Empty;
    using static SharpIOLib.Result;
    using static SharpIOLib.SharpIO;

    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("🌄");
        }

        public static IO<E, Exception, Empty> GetProgram<E>()
            where E : ILog<E>, IConsole<E> =>
            from logStart in Log.Info<E>("Starting the quiz...")
            from askForName in ConsoleX.WriteLine<E>("Enter your name:")
            from name in ConsoleX.ReadLine<E>()
            //from logName in Log.Info<E>($"Name is @{name}")
            //from writeName in ConsoleX.WriteLine<E>($"Name is {name}") 
            select empty;
    }

    public interface IConsole<in _>
    {
        IO<_, CannotFail, Empty> WriteLine(string line);
        IO<_, Exception, string> ReadLine();
    }

    public static class ConsoleX
    {
        public static IO<E, CannotFail, Empty> WriteLine<E>(string line) where E : IConsole<E> =>
            Env<E>().BindFromCannotFail(x => x.WriteLine(line));

        public static IO<E, Exception, string> ReadLine<E>() where E : IConsole<E> =>
            Env<E, Exception>().Bind(x => x.ReadLine());
    }

    public interface ILog<in _>
    {
        IO<_, CannotFail, Empty> Info(string message);
    }

    public static class Log
    {
        public static IO<R, CannotFail, Empty> Info<R>(string message) where R : ILog<R> =>
            Env<R>().BindFromCannotFail(x => x.Info(message));
    }

    public class TestConsole<_> : IConsole<_>
    {
        public IO<_, CannotFail, Empty> WriteLine(string line) => Empty<_>();

        public IO<_, Exception, string> ReadLine() => Success("hey", default(Exception)).ToIO; //IO<_, Exception, string>("hey");
    }

    public class TestLog<_> : ILog<_>
    {
        public IO<_, CannotFail, Empty> Info(string message) => Empty<_>();
    }
}

namespace SharpIOLib
{
    using System;
    using static Empty;
    using static Result;

    // ReSharper disable UnusedTypeParameter

    public abstract class CannotFail
    {
        private CannotFail() { }
    }

    public readonly struct Empty
    {
        public static readonly Empty empty = new Empty();
    }

    public delegate IResult<TErr, TVal> IO<in TEnv, out TErr, out TVal>(TEnv env);

    //public interface IO<in R, out E, out A>
    //{
    //    IResult<E, A> Run(R runtime);
    //}

    //public struct SIO<R, E, A> : IO<R, E, A>
    //{
    //    public Func<R, IResult<E, A>> Runner;
    //    public SIO(Func<R, IResult<E, A>> runner) => Runner = runner;
    //    public IResult<E, A> Run(R runtime) => Runner(runtime);
    //}

    public interface IResult<out TErr, out TVal> { }

    public struct Success<TErr, TVal> : IResult<TErr, TVal>
    {
        public readonly TVal Value;
        public Success(TVal value) => Value = value;
    }

    public struct Failure<TErr, TVal> : IResult<TErr, TVal>
    {
        public readonly TErr Error;
        public Failure(TErr error) => Error = error;

        public Failure<TErr, TOther> FailFor<TOther>() => new Failure<TErr, TOther>(Error);
    }

    public static class Result
    {
        public static IResult<TErr, TVal> Success<TErr, TVal>(TVal value, TErr _ = default) => new Success<TErr, TVal>(value);

        public static IResult<TErr, Empty> Success<TErr>() => new Success<TErr, Empty>(empty);

        public static IResult<CannotFail, TVal> Success<TVal>(TVal value) => new Success<CannotFail, TVal>(value);

        public static IResult<CannotFail, Empty> Success() => SuccessEmpty;
        public static readonly Success<CannotFail, Empty> SuccessEmpty = new Success<CannotFail, Empty>(empty);

        public static IResult<TErr, TVal> Failure<TErr, TVal>(TErr error, TVal _ = default) => new Failure<TErr, TVal>(error);

        public static IResult<TErr, Empty> Failure<TErr>(TErr error) => new Failure<TErr, Empty>(error);

        public static IResult<TErr, B> Map<TErr, A, B>(this IResult<TErr, A> a, Func<A, B> map) =>
            a is Success<TErr, A> success ? Success<TErr, B>(map(success.Value)) : ((Failure<TErr, A>)a).FailFor<B>();
    }

    // todo: rename to IO or EnvIO
    public static class SharpIO
    {
        public static IResult<TErr, TVal> ToIO<TEnv, TErr, TVal>(this IResult<TErr, TVal> result, TEnv _ = default) => result;

        public static IO<TEnv, TErr, TVal> ToIO<TEnv, TErr, TVal>(this TVal value, TErr _ = default, TEnv __ = default) => Success(value, default(TErr)).ToIO;

        public static IO<TEnv, TErr, Empty> Empty<TEnv, TErr>() => Success<TErr>().ToIO;

        public static IO<TEnv, CannotFail, Empty> Empty<TEnv>() => Success().ToIO;

        public static IO<TEnv, TErr, TEnv> Env<TEnv, TErr>() => env => Success(env, default(TErr));

        public static IO<TEnv, CannotFail, TEnv> Env<TEnv>() => Success;

        public static IO<TEnv, TErr, B> Map<TEnv, TErr, A, B>(this IO<TEnv, TErr, A> a, Func<A, B> map) => env => a(env).Map(map);

        public static IO<TEnv, TErr, B> Bind<TEnv, TErr, A, B>(this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, TErr, B>> map) =>
            env =>
            {
                var result = a(env);
                return result is Success<TErr, A> success ? map(success.Value)(env) : ((Failure<TErr, A>)result).FailFor<B>().ToIO(env);
            };

        public static IO<TEnv, TErr, B> BindFromCannotFail<TEnv, TErr, A, B>(this IO<TEnv, CannotFail, A> a, Func<A, IO<TEnv, TErr, B>> map) =>
          env => map(((Success<CannotFail, A>)a(env)).Value)(env);

        public static IO<TEnv, TErr, B> BindToCannotFail<TEnv, TErr, A, B>(this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, CannotFail, B>> map) =>
            env =>
            {
                var result = a(env);
                return result is Success<TErr, A> success
                    ? Success<TErr, B>(((Success<CannotFail, B>)map(success.Value)(env)).Value)
                    : ((Failure<TErr, A>)result).FailFor<B>().ToIO(env);
            };
    }

    public static class LinqIOExtensions
    {
        public static IO<TEnv, TErr, B> Select<TEnv, TErr, A, B>(this IO<TEnv, TErr, A> a, Func<A, B> map) => a.Map(map);

        public static IO<TEnv, TErr, C> SelectMany<TEnv, TErr, A, B, C>(this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, TErr, B>> map, Func<A, B, C> project) =>
            a.Bind(x => map(x).Bind(y => project(x, y).ToIO<TEnv, TErr, C>()));

        public static IO<TEnv, TErr, C> SelectMany<TEnv, TErr, A, B, C>(this IO<TEnv, CannotFail, A> a, Func<A, IO<TEnv, TErr, B>> map, Func<A, B, C> project) =>
            a.BindFromCannotFail(x => map(x).Bind(y => project(x, y).ToIO<TEnv, TErr, C>()));

        //public static IO<TEnv, TErr, C> SelectMany<TEnv, TErr, A, B, C>(this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, CannotFail, B>> map, Func<A, B, C> project) =>
        //    a.BindToCannotFail(x => map(x).Bind(y => project(x, y).ToIO<TEnv, CannotFail, C>()));
    }
}
