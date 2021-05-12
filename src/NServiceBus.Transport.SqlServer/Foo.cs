#pragma warning disable MA0048 // File name must match type name
namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    class Foo
    {
        public Foo()
        { }

        public Foo(CancellationToken cancellationToken) { }

        protected void Bar<T>() { Bar(CancellationToken.None); }
        protected void Bar(CancellationToken cancellationToken = default) { _ = GetType(); }
    }

    class Test : Foo
    {
        public async Task Something(CancellationToken cancellationToken)
        {

            _ = new Foo();
            Bar();

            Extensions.Baz(123);

            await foreach (var item in GenerateSequenceAsync().ConfigureAwait(false))
            {
                Console.WriteLine(item);
            }

            Func<CancellationToken, bool> foo = token =>
                {
                    Bar();
                    return true;
                }
        }

        IAsyncEnumerable<object> GenerateSequenceAsync() => throw new NotSupportedException();
    }

    static class Extensions
    {
        public static void Baz(int x) { }

        public static void Baz(int y, CancellationToken cancellationToken = default) { }
    }
}
