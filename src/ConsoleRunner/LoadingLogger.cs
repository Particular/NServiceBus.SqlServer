public static class LoadingLogger
{
    public static bool Active;

    static LoadingLogger()
    {
        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        AppDomain.CurrentDomain.TypeResolve += CurrentDomain_TypeResolve;
    }

    static System.Reflection.Assembly? CurrentDomain_TypeResolve(object? sender, ResolveEventArgs args)
    {
        if (Active)
        {
            Console.Write(">>> ");
            Console.WriteLine(Ansi("Type resolve: {0} [{1}]"), args.Name, args.RequestingAssembly);
        }

        return null;
    }

    static System.Reflection.Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        if (Active)
        {
            Console.Write(">>> ");
            Console.WriteLine(Ansi("Assembly resolve: {0} [{1}]"), args.Name, args.RequestingAssembly);
        }
        return null;

    }

    static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        if (Active)
        {
            Console.Write(">>> ");
            Console.WriteLine(Ansi("Assembly load: {0}"), args.LoadedAssembly);
        }
    }

    static string Ansi(string text, int color = 36)
    {
        return $"\x1b[{color}m{text}\x1b[0m";
    }
}