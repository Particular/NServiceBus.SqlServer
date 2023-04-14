namespace TestRunner
{
    using System;
    using System.Collections.Generic;

    public class AuditMessage
    {
        public AuditMessage(string id, Dictionary<string, string> headers, ReadOnlyMemory<byte> body)
        {
            Id = id;
            Headers = headers;
            Body = body;
        }

        public ReadOnlyMemory<byte> Body { get; }
        public Dictionary<string, string> Headers { get; }
        public string Id { get; }
    }
}