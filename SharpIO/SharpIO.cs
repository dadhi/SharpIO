using System;
using static NoError;
using static NoValue;
using static SharpIO;

public class Program
{
    public static void Main()
    {



        Console.WriteLine("🌄");
    }

    // ## The user program

    public static IO<R, Exception, NoValue> GetProgram<R>()
        where R : ILog<R>, IConsole<R> =>
        from logStart in Log.Info<R>("Starting the quiz...")
        from askForName in ConsoleX.WriteLine<R>("Enter your name:")
        from name in ConsoleX.ReadLine<R>()
        //from logName in Log.Info<R>($"Name is {name}")
        select noValue;
}

public interface IConsole<in _>
{
    IO<_, NoError, NoValue> WriteLine(string line);
    IO<_, Exception, string> ReadLine();
}

public static class ConsoleX
{
    public static IO<R, NoError, NoValue> WriteLine<R>(string line)
        where R : IConsole<R> =>
        Runtime<R, NoError>().Bind(r => r.WriteLine(line));

    public static IO<R, Exception, string> ReadLine<R>()
        where R : IConsole<R> =>
        Runtime<R, Exception>().Bind(r => r.ReadLine());
}

public interface ILog<in _>
{
    IO<_, NoError, NoValue> Info(string message);
}

public static class Log
{
    public static IO<R, NoError, NoValue> Info<R>(string message)
        where R : ILog<R> =>
        Runtime<R, NoError>().Bind(c => c.Info(message));
}

public class TestConsole<_> : IConsole<_>
{
    public IO<_, NoError, NoValue> WriteLine(
        string line) => noValue.ToIO<_, NoError>();
    public IO<_, Exception, string> ReadLine() =>
        "hey".ToIO<_, Exception, string>();
}

public class TestLog<_> : ILog<_>
{
    public IO<_, NoError, NoValue> Info(
        string message) => noValue.ToIO<_, NoError>();
}

// ## SharpIO library: 

public struct NoError
{
    public static readonly NoError noError = new NoError();
}

public struct NoValue
{
    public static readonly NoValue noValue = new NoValue();
}

public interface IO<in R, out E, out A>
{
    IResult<E, A> Run(R runtime);
}

public struct SIO<R, E, A> : IO<R, E, A>
{
    public Func<R, IResult<E, A>> Runner;
    public SIO(Func<R, IResult<E, A>> runner) => Runner = runner;
    public IResult<E, A> Run(
        R runtime) => Runner(runtime);
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

    public static Success<NoError, A> Success<A>(
        A a) => new Success<NoError, A>(a);

    public static IResult<E, A> Failure<E, A>(
        E e, A _ = default) => new Failure<E, A>(e);

    public static IResult<E, A> RunIO<R, E, A>(
        this IResult<E, A> a, R runtime = default) => a;

    public static IO<R, E, A> ToIO<R, E, A>(
        this A a, E _ = default, R __ = default) =>
        new SIO<R, E, A>(Success(a, _).RunIO);

    public static IO<R, E, NoValue> ToIO<R, E>(
        this NoValue none, R runtime = default) =>
        new SIO<R, E, NoValue>(Success(noValue, default(E)).RunIO);

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

    public static IO<R, E, B> BindNoError<R, E, A, B>(
      this IO<R, NoError, A> a, Func<A, IO<R, E, B>> map) =>
      new SIO<R, E, B>(r =>
          map(((Success<NoError, A>)a.Run(r)).Value)
          .Run(r));

    public static IO<R, E, B> Select<R, E, A, B>(this IO<R, E, A> a, Func<A, B> map) =>
        a.To(map);

    public static IO<R, E, C> SelectMany<R, E, A, B, C>(this IO<R, E, A> a, Func<A, IO<R, E, B>> map, Func<A, B, C> project) =>
        a.Bind(x => map(x).Bind(y => project(x, y).ToIO<R, E, C>()));

    public static IO<R, E, C> SelectMany<R, E, A, B, C>(this IO<R, NoError, A> a, Func<A, IO<R, E, B>> map, Func<A, B, C> project) =>
        a.BindNoError(x => map(x).Bind(y => project(x, y).ToIO<R, E, C>()));
}

