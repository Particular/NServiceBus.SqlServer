namespace NServiceBus.Transport.SqlServer
{
    using System.Threading;

    class Foo
    {
        protected void Bar<T>() { Bar(CancellationToken.None); }
        protected void Bar(CancellationToken cancellationToken = default) { _ = GetType(); }
    }

    class Test : Foo
    {
        public void Something(CancellationToken cancellationToken = default)
        {
            Bar(cancellationToken);

            Extensions.Baz(123);
        }
    }

    static class Extensions
    {
        public static void Baz(int x) { }

        public static void Baz(int y, CancellationToken cancellationToken = default) { }
    }
}
