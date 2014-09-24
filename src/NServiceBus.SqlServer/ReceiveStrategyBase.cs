namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Globalization;
    using System.Transactions;
    using NServiceBus.Pipeline;

    class ReceiveStrategyBase
    {
        protected ReceiveStrategyBase(Func<TransportMessage, bool> tryProcessMessage, TransportMessageReader transportMessageReader, string connectionString, PipelineExecutor pipelineExecutor, TransactionOptions transactionOptions)
        {
            this.tryProcessMessage = tryProcessMessage;
            this.transportMessageReader = transportMessageReader;
            this.connectionString = connectionString;
            this.pipelineExecutor = pipelineExecutor;
            this.transactionOptions = transactionOptions;
        }

        protected string GetQueryForTable(string tableName)
        {
            return string.Format(CultureInfo.InvariantCulture, SqlReceive, tableName);
        }

        const string SqlReceive =
            @"WITH message AS (SELECT TOP(1) * FROM [{0}] WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowVersion] ASC) 
			DELETE FROM message 
			OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, 
			deleted.Recoverable, deleted.Expires, deleted.Headers, deleted.Body;";

        protected Func<TransportMessage, bool> tryProcessMessage;
        protected TransportMessageReader transportMessageReader;
        protected String connectionString;
        protected PipelineExecutor pipelineExecutor;
        protected TransactionOptions transactionOptions;
    }
}