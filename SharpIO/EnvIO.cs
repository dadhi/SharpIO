using System;
using SharpIO;
using static SharpIO.Result;
using static SharpIO.IO;

namespace SharpIODemo
{
    public class Program
    {
        public static void Main()
        {
            var game = CreateGame();

            var expected = game.Run(new Env(new TestConsole<Env>("OnepunchMan"), new TestLog<Env>()));

            Console.WriteLine(expected);
        }

        public static IO<Env, Exception, string> CreateGame() =>
            from logStart in Log<Env>.Info("Starting the quiz...")
            from askForName in Out<Env>.WriteLine("Enter your name:")
            from name in Out<Env>.ReadLine()
            from logName in Log<Env>.Info($"Your name is {name}")
            select name;

        public sealed class Env : IWithLog<Env>, IWithConsole<Env>
        {
            public IConsole<Env> Console { get; }
            public ILog<Env> Log { get; }
            public Env(IConsole<Env> console, ILog<Env> log) => (Console, Log) = (console, log).EnsureNotNull();
        }
    }

    public interface IConsole<in TEnv>
    {
        IO<TEnv, Nothing> WriteLine(string line);
        IO<TEnv, Exception, string> ReadLine();
    }

    public interface IWithConsole<in TEnv>
    {
        IConsole<TEnv> Console { get; }
    }

    public interface ILog<in TEnv>
    {
        IO<TEnv, Nothing> Info(string message);
    }

    public interface IWithLog<in TEnv>
    {
        ILog<TEnv> Log { get; }
    }

    public class TestConsole<TEnv> : IConsole<TEnv>
    {
        private readonly string _output;

        public TestConsole(string output = default) => _output = output ?? "Wizard";

        public IO<TEnv, Nothing> WriteLine(string line) => DoNothing<TEnv>();

        public IO<TEnv, Exception, string> ReadLine() =>
            Success(_output, default(Exception)).ToIO<TEnv>();
    }

    public class TestLog<TEnv> : ILog<TEnv>
    {
        public IO<TEnv, Nothing> Info(string message) => DoNothing<TEnv>();
    }

    public static class Out<TEnv> where TEnv : IWithConsole<TEnv>
    {
        public static IO<TEnv, Nothing> WriteLine(string line) =>
            Use<TEnv>().To(e => e.Console.WriteLine(line));

        public static IO<TEnv, Exception, string> ReadLine() =>
            Use<TEnv, Exception>().To(e => e.Console.ReadLine());
    }

    public static class Log<TEnv> where TEnv : IWithLog<TEnv>
    {
        public static IO<TEnv, Nothing> Info(string message) =>
            Use<TEnv>().To(e => e.Log.Info(message));
    }
}

namespace SharpIO
{
    using System;
    using static Nothing;
    using static Result;

    // ReSharper disable UnusedTypeParameter

    public abstract class CannotFail
    {
        private CannotFail() { }
    }

    public readonly struct Nothing
    {
        public static readonly Nothing nothing = new Nothing();
    }

    public interface IResult<out TErr, out TVal> { }

    public readonly struct Success<TErr, TVal> : IResult<TErr, TVal>
    {
        public readonly TVal Value;
        public Success(TVal value) => Value = value;

        /// Helping the inference
        public IO<TEnv, TErr, TVal> ToIO<TEnv>() => this.ToIO<TEnv, TErr, TVal>();

        public override string ToString() => "success: " + Value;
    }

    public readonly struct Failure<TErr, TVal> : IResult<TErr, TVal>
    {
        public readonly TErr Error;
        public Failure(TErr error) => Error = error;

        public Failure<TErr, TOther> FailFor<TOther>() => new Failure<TErr, TOther>(Error);

        public override string ToString() => "failure! " + Error;
    }

    public static class Result
    {
        public static Success<TErr, TVal> Success<TErr, TVal>(TVal value, TErr _ = default) => 
            new Success<TErr, TVal>(value);

        public static Success<TErr, Nothing> Success<TErr>() => 
            new Success<TErr, Nothing>(nothing);

        public static IResult<CannotFail, TVal> Success<TVal>(TVal value) => 
            new Success<CannotFail, TVal>(value);

        public static Success<CannotFail, Nothing> Success() => SuccessEmpty;
        public static readonly Success<CannotFail, Nothing> SuccessEmpty = 
            new Success<CannotFail, Nothing>(nothing);

        public static IResult<TErr, TVal> Failure<TErr, TVal>(TErr error, TVal _ = default) => 
            new Failure<TErr, TVal>(error);

        public static IResult<TErr, Nothing> Failure<TErr>(TErr error) => 
            new Failure<TErr, Nothing>(error);

        public static IResult<TErr, B> Map<TErr, A, B>(this IResult<TErr, A> a, Func<A, B> map) =>
            a is Success<TErr, A> success 
                ? (IResult<TErr, B>)Success<TErr, B>(map(success.Value)) 
                : ((Failure<TErr, A>)a).FailFor<B>();
    }

    public interface IO<in TEnv, out TErr, out TVal>
    {
        Func<TEnv, IResult<TErr, TVal>> Run { get; }
    }

    public interface IO<in TEnv, out TVal> : IO<TEnv, CannotFail, TVal> {}

    public interface IO<in TEnv> : IO<TEnv, CannotFail, Nothing> {}

    public struct EnvIO<TEnv, TErr, TVal> : IO<TEnv, TErr, TVal>
    {
        public Func<TEnv, IResult<TErr, TVal>> Run { get; }
        public EnvIO(Func<TEnv, IResult<TErr, TVal>> run) => Run = run;
    }

    public struct EnvIO<TEnv, TVal> : IO<TEnv, TVal>
    {
        public Func<TEnv, IResult<CannotFail, TVal>> Run { get; }
        public EnvIO(Func<TEnv, IResult<CannotFail, TVal>> run) => Run = run;
    }

    public static class IO
    {
        static IResult<TErr, TVal> Run<TEnv, TErr, TVal>(this IResult<TErr, TVal> result, TEnv _ = default) => 
            result;

        public static IO<TEnv, TErr, TVal> ToIO<TEnv, TErr, TVal>(this IResult<TErr, TVal> result) =>
            new EnvIO<TEnv, TErr, TVal>(result.Run);

        public static IO<TEnv, TErr, Nothing> DoNothingOrFail<TEnv, TErr>() => 
            new EnvIO<TEnv, TErr, Nothing>(((IResult<TErr, Nothing>)Success<TErr>()).Run);

        public static IO<TEnv, Nothing> DoNothing<TEnv>() => 
            new EnvIO<TEnv, Nothing>(((IResult<CannotFail, Nothing>)Success()).Run);

        public static IO<TEnv, TErr, TEnv> Use<TEnv, TErr>() => 
            new EnvIO<TEnv, TErr, TEnv>(env => Success(env, default(TErr)));

        public static IO<TEnv, TEnv> Use<TEnv>() => 
            new EnvIO<TEnv, TEnv>(Success);

        public static IO<TEnv, B> To<TEnv, A, B>(
            this IO<TEnv, A> aCannotFail, Func<A, IO<TEnv, B>> map) =>
            new EnvIO<TEnv, B>(env =>
            {
                var aSuc = (Success<CannotFail, A>)aCannotFail.Run(env);
                var b = map(aSuc.Value);
                return b.Run(env);
            });

        public static IO<TEnv, TErr, B> To<TEnv, TErr, A, B>(
            this IO<TEnv, A> aCannotFail, Func<A, IO<TEnv, TErr, B>> map) =>
            new EnvIO<TEnv, TErr, B>(env =>
            {
                var aSuc = (Success<CannotFail, A>)aCannotFail.Run(env);
                var b = map(aSuc.Value);
                return b.Run(env);
            });

        public static IO<TEnv, TErr, B> To<TEnv, TErr, A, B>(
            this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, B>> map) =>
            new EnvIO<TEnv, TErr, B>(env =>
            {
                var aRes = a.Run(env);
                if (aRes is Success<TErr, A> aSuc)
                {
                    var bCannotFail = map(aSuc.Value);
                    var bSuc = (Success<CannotFail, B>)bCannotFail.Run(env);
                    return new Success<TErr, B>(bSuc.Value);
                }

                return ((Failure<TErr, A>)aRes).FailFor<B>();
            });

        public static IO<TEnv, TErr, B> To<TEnv, TErr, A, B>(
            this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, TErr, B>> map) =>
            new EnvIO<TEnv, TErr, B>(env =>
            {
                var aRes = a.Run(env);
                if (aRes is Success<TErr, A> aSuc)
                    return map(aSuc.Value).Run(env);

                return ((Failure<TErr, A>)aRes).FailFor<B>();
            });
    }

    public static class IOLinq
    {
        public static IO<TEnv, TErr, B> Select<TEnv, TErr, A, B>(
            this IO<TEnv, TErr, A> a, Func<A, B> map) => 
            new EnvIO<TEnv, TErr, B>(env => a.Run(env).Map(map));

        public static IO<TEnv, C> SelectMany<TEnv, A, B, C>(
            this IO<TEnv, A> aCannotFail, Func<A, IO<TEnv, B>> map, Func<A, B, C> project)
        {
            return new EnvIO<TEnv, C>(env =>
            {
                var aSuc = (Success<CannotFail, A>)aCannotFail.Run(env);
                var b = map(aSuc.Value);
                var bSuc = (Success<CannotFail, B>)b.Run(env);
                return new Success<CannotFail, C>(project(aSuc.Value, bSuc.Value));
            });
        }

        public static IO<TEnv, TErr, C> SelectMany<TEnv, TErr, A, B, C>(
            this IO<TEnv, A> aCannotFail, Func<A, IO<TEnv, TErr, B>> map, Func<A, B, C> project)
        {
            return new EnvIO<TEnv, TErr, C>(env =>
            {
                var aSuc = (Success<CannotFail, A>)aCannotFail.Run(env);
                var b = map(aSuc.Value);
                var bRes = b.Run(env);
                if (bRes is Success<TErr, B> bSuc)
                    return new Success<TErr, C>(project(aSuc.Value, bSuc.Value));

                return ((Failure<TErr, B>)bRes).FailFor<C>();
            }); 
        }

        public static IO<TEnv, TErr, C> SelectMany<TEnv, TErr, A, B, C>(
            this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, B>> map, Func<A, B, C> project) =>
            new EnvIO<TEnv, TErr, C>(env =>
            {
                var aRes = a.Run(env);
                if (aRes is Success<TErr, A> aSuc)
                {
                    var bCannotFail = map(aSuc.Value);
                    var bSuc = (Success<CannotFail, B>)bCannotFail.Run(env);
                    return new Success<TErr, C>(project(aSuc.Value, bSuc.Value));
                }

                return ((Failure<TErr, A>)aRes).FailFor<C>();
            });

        public static IO<TEnv, TErr, C> SelectMany<TEnv, TErr, A, B, C>(
            this IO<TEnv, TErr, A> a, Func<A, IO<TEnv, TErr, B>> map, Func<A, B, C> project) =>
            new EnvIO<TEnv, TErr, C>(env =>
            {
                var aRes = a.Run(env);
                if (aRes is Success<TErr, A> aSuc)
                {
                    var b = map(aSuc.Value);
                    var bRes = b.Run(env);
                    if (bRes is Success<TErr, B> bSuc)
                        return new Success<TErr, C>(project(aSuc.Value, bSuc.Value));

                    return ((Failure<TErr, B>)bRes).FailFor<C>();
                }

                return ((Failure<TErr, A>)aRes).FailFor<C>();
            });
    }

    public static class Ensure
    {
        public static (T1, T2) EnsureNotNull<T1, T2>(in this (T1, T2) x) =>
            x.Item1 is null ? throw new ArgumentNullException("???", $"arg0 of '{typeof(T1)}' should not be null") :
            x.Item2 is null ? throw new ArgumentNullException("???", $"arg1 is '{typeof(T2)}' should not be null") :
            x;
    }
}
