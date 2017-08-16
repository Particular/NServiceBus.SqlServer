namespace NServiceBus.Transport.SQLServer
{
    class NameHelper
    {
        const string prefix = "[";
        const string suffix = "]";

        public static string Quote(string unquotedName)
        {
            if (unquotedName == null)
            {
                return null;
            }
            return prefix + unquotedName.Replace(suffix, suffix + suffix) + suffix;
        }
    }
}