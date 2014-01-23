namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    public static class TableNameUtils
    {
        public static string GetTableName(Address address)
        {
            if (address.Queue.Length > 128)
            {
                return DeterministicGuidBuilder(address.Queue).ToString();
            }

            return address.Queue;
        }

        private static Guid DeterministicGuidBuilder(string input)
        {
            // use MD5 hash to get a 16-byte hash of the string
            using (var provider = new MD5CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                var hashBytes = provider.ComputeHash(inputBytes);
                // generate a guid from the hash:
                return new Guid(hashBytes);
            }
        }
    }
}