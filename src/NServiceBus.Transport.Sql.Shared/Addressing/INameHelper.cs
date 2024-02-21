namespace NServiceBus.Transport.Sql.Shared.Addressing;

public interface INameHelper
{
    string Quote(string unquotedName);
    string Unquote(string quotedString);
}