namespace NServiceBus.Transports.SQLServer.Light
{
    using System.Collections.Generic;
    using System.IO;

    class SqlMessage
    {
        public string Id { get; set; }
        public Stream BodyStream { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public SqlMessage(string id, Stream bodyStream, Dictionary<string, string> headers)
        {
            Id = id;
            BodyStream = bodyStream;
            Headers = headers;
        }
    }
}