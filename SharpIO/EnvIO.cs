namespace SharpIODemo
{
    using System;
    using SharpIO;
    using static SharpIO.Empty;
    using static SharpIO.Result;
    using static SharpIO.IO;

    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("hey");
        }

        public static IO<TEnv, Exception, Empty> GetProgram<TEnv>()
            where TEnv : ILog<TEnv>, IConsole<TEnv>
        {
            return from logStart in Log.Info<TEnv>("Starting the quiz...")
                from askForName in Out.WriteLine<TEnv>("Enter your name:")
                from name in Out.ReadLine<TEnv>()
                from logName in Log.Info<TEnv>($"Name is @{name}")
                select empty;
        }
    }

    public interface IConsole<in TEnv>
    {
        IO<TEnv, Empty> WriteLine(string line);

        IO<TEnv, Exception, string> ReadLine();
    }

    public static class Out
    {
        public static IO<E, Empty> WriteLine<E>(string line) where E : IConsole<E> =>
            Use<E>().To(e => e.WriteLine(line));

        public static IO<E, Exception, string> ReadLine<E>() where E : IConsole<E> =>
            Use<E, Exception>().To(e => e.ReadLine());
    }

    public static class Out<TEnv> where TEnv : IConsole<TEnv>
    {
        public static IO<TEnv, Empty> WriteLine(string line) =>
            Use<TEnv>().To(e => e.WriteLine(line));

        public static IO<TEnv, Exception, string> ReadLine() =>
            Use<TEnv, Exception>().To(e => e.ReadLine());
    }

    public interface ILog<in TEnv>
    {
        IO<TEnv, Empty> Info(string message);
    }

    public static class Log
    {
        public static IO<TEnv, Empty> Info<TEnv>(string message) where TEnv : ILog<TEnv> =>
            Use<TEnv>().To(e => e.Info(message));
    }

    public class TestOut<TEnv> : IConsole<TEnv>
    {
        public IO<TEnv, Empty> WriteLine(string line) => Empty<TEnv>();

        public IO<TEnv, Exception, string> ReadLine() => 
            Success("hey", default(Exception)).ToIO<TEnv, Exception, string>(); // todo: help inference
    }

    public class TestLog<TEnv> : ILog<TEnv>
    {
        public IO<TEnv, Empty> Info(string message) => Empty<TEnv>();
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
    }

    public struct Failure<TErr, TVal> : IResult<TErr, TVal>
    {
        public readonly TErr Error;
        public Failure(TErr error) => Error = error;

        public Failure<TErr, TOther> FailFor<TOther>() => new Failure<TErr, TOther>(Error);
    }

    public static class Result
    {
        public static IResult<TErr, TVal> Success<TErr, TVal>(TVal value, TErr _ = default) => 
            new Success<TErr, TVal>(value);

        public static IResult<TErr, Empty> Success<TErr>() => 
            new Success<TErr, Empty>(empty);

        public static IResult<CannotFail, TVal> Success<TVal>(TVal value) => 
            new Success<CannotFail, TVal>(value);

        public static IResult<CannotFail, Empty> Success() => SuccessEmpty;
        public static readonly Success<CannotFail, Empty> SuccessEmpty = 
            new Success<CannotFail, Empty>(empty);

        public static IResult<TErr, TVal> Failure<TErr, TVal>(TErr error, TVal _ = default) => 
            new Failure<TErr, TVal>(error);

        public static IResult<TErr, Empty> Failure<TErr>(TErr error) => 
            new Failure<TErr, Empty>(error);

        public static IResult<TErr, B> Map<TErr, A, B>(this IResult<TErr, A> a, Func<A, B> map) =>
            a is Success<TErr, A> success ? Success<TErr, B>(map(success.Value)) : ((Failure<TErr, A>)a).FailFor<B>();
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
        public static IResult<TErr, TVal> Run<TEnv, TErr, TVal>(this IResult<TErr, TVal> result, TEnv _ = default) => 
            result;

        public static IO<TEnv, TErr, TVal> ToIO<TEnv, TErr, TVal>(this IResult<TErr, TVal> result) =>
            new EnvIO<TEnv, TErr, TVal>(result.Run);

        public static IO<TEnv, TErr, TVal> ToIO<TEnv, TErr, TVal>(this TVal value, TErr _ = default, TEnv __ = default) =>
            new EnvIO<TEnv, TErr, TVal>(Success(value, default(TErr)).Run);

        public static IO<TEnv, TVal> ToIO<TEnv, TVal>(this TVal value, TEnv _ = default) =>
            new EnvIO<TEnv, TVal>(Success(value).Run);

        public static IO<TEnv, TErr, Empty> Empty<TEnv, TErr>() => 
            new EnvIO<TEnv, TErr, Empty>(Success<TErr>().Run);

        public static IO<TEnv, Empty> Empty<TEnv>() => 
            new EnvIO<TEnv, Empty>(Success().Run);

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
