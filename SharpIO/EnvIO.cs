namespace SharpIODemo
{
    using System;
    using SharpIO;
    using static SharpIO.Result;
    using static SharpIO.IO;

    public class Program
    {
        public static void Main()
        {
            var game = CreateGame();

            var expected = game.Run(new Env(new TestOut<Env>("OnepunchMan"), new TestLog<Env>()));

            Console.WriteLine(expected);
        }

        public static IO<Env, Exception, string> CreateGame() =>
            from logStart in Log<Env>.Info("Starting the quiz...")
            from askForName in Out<Env>.WriteLine("Enter your name:")
            from name in Out<Env>.ReadLine()
            from logName in Log<Env>.Info($"Your name is {name}")
            select name;

        public sealed class Env : IWithLog<Env>, IWithOut<Env>
        {
            public IOut<Env> Out { get; }
            public ILog<Env> Log { get; }

            public Env(IOut<Env> @out, ILog<Env> log) => (Out, Log) = (@out, log);
        }
    }

    public class TestOut<E> : IOut<E>
    {
        private readonly string _output;

        public TestOut(string output = default) => _output = output ?? "Wizard";

        public IO<E, Empty> WriteLine(string line) => EmptyIO<E>();

        public IO<E, Exception, string> ReadLine() =>
            Success(_output, default(Exception)).ToIO<E>();
    }

    public class TestLog<E> : ILog<E>
    {
        public IO<E, Empty> Info(string message) => EmptyIO<E>();
    }

    public interface IOut<in E>
    {
        IO<E, Empty> WriteLine(string line);

        IO<E, Exception, string> ReadLine();
    }

    public interface IWithOut<in E>
    {
        IOut<E> Out { get; }
    }

    public static class Out<E> where E : IWithOut<E>
    {
        public static IO<E, Empty> WriteLine(string line) =>
            Use<E>().To(e => e.Out.WriteLine(line));

        public static IO<E, Exception, string> ReadLine() =>
            Use<E, Exception>().To(e => e.Out.ReadLine());
    }

    public interface ILog<in E>
    {
        IO<E, Empty> Info(string message);
    }

    public interface IWithLog<in E>
    {
        ILog<E> Log { get; }
    }

    public static class Log<E> where E : IWithLog<E>
    {
        public static IO<E, Empty> Info(string message) =>
            Use<E>().To(e => e.Log.Info(message));
    }
}

namespace SharpIO
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

    public interface IResult<out TErr, out TVal> { }

    public struct Success<TErr, TVal> : IResult<TErr, TVal>
    {
        public readonly TVal Value;
        public Success(TVal value) => Value = value;

        /// Cheating a bit to help the inference
        public IO<TEnv, TErr, TVal> ToIO<TEnv>() => this.ToIO<TEnv, TErr, TVal>();

        public override string ToString() => "success: " + Value;
    }

    public struct Failure<TErr, TVal> : IResult<TErr, TVal>
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

        public static Success<TErr, Empty> Success<TErr>() => 
            new Success<TErr, Empty>(empty);

        public static IResult<CannotFail, TVal> Success<TVal>(TVal value) => 
            new Success<CannotFail, TVal>(value);

        public static Success<CannotFail, Empty> Success() => SuccessEmpty;
        public static readonly Success<CannotFail, Empty> SuccessEmpty = 
            new Success<CannotFail, Empty>(empty);

        public static IResult<TErr, TVal> Failure<TErr, TVal>(TErr error, TVal _ = default) => 
            new Failure<TErr, TVal>(error);

        public static IResult<TErr, Empty> Failure<TErr>(TErr error) => 
            new Failure<TErr, Empty>(error);

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

        public static IO<TEnv, TErr, Empty> Empty<TEnv, TErr>() => 
            new EnvIO<TEnv, TErr, Empty>(((IResult<TErr, Empty>)Success<TErr>()).Run);

        public static IO<TEnv, Empty> EmptyIO<TEnv>() => 
            new EnvIO<TEnv, Empty>(((IResult<CannotFail, Empty>)Success()).Run);

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
}
