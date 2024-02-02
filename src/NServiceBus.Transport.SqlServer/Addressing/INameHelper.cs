namespace NServiceBus.Transport.SqlServer;

interface INameHelper
{
    string Quote(string unquotedName);
    string Unquote(string quotedString);
}