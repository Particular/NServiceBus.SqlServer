:setvar QueueName "MyNewQueueName"

IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[$(QueueName)]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[$(QueueName)](
		[Id] [uniqueidentifier] NOT NULL,
		[CorrelationId] [varchar](255) NULL,
		[ReplyToAddress] [varchar](255) NULL,
		[Recoverable] [bit] NOT NULL,
		[Expires] [datetime] NULL,
		[Headers] [varchar](max) NOT NULL,
		[Body] [varbinary](max) NULL,
		[RowVersion] [bigint] IDENTITY(1,1) NOT NULL
	) ON [PRIMARY];                    

	CREATE CLUSTERED INDEX [Index_RowVersion] ON [dbo].[$(QueueName)] 
	(
		[RowVersion] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];            
END
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SendTo_$(QueueName)]') AND type in (N'P')) 
BEGIN
	DROP PROCEDURE [SendTo_$(QueueName)]
END
GO

CREATE PROCEDURE [SendTo_$(QueueName)]
(
	@Id [uniqueidentifier],
	@CorrelationId [varchar](255),
	@ReplyToAddress [varchar](255),
	@Recoverable [bit],
	@Expires [datetime],
	@Headers [varchar](max),
	@Body [varbinary](max)
)
AS
BEGIN
	INSERT INTO [$(QueueName)] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body]) 
	VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)
END;
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReceiveFrom_$(QueueName)]') AND type in (N'P')) 
BEGIN
	DROP PROCEDURE [ReceiveFrom_$(QueueName)]
END
GO

CREATE PROCEDURE [ReceiveFrom_$(QueueName)]
AS
BEGIN 
	WITH message AS (SELECT TOP(1) * FROM [$(QueueName)] WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowVersion] ASC) 
	DELETE FROM message 
	OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, 
	deleted.Recoverable, deleted.Expires, deleted.Headers, deleted.Body;
END
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PurgeFrom_$(QueueName)]') AND type in (N'P')) 
BEGIN
	DROP PROCEDURE [PurgeFrom_$(QueueName)]
END
GO

CREATE PROCEDURE [PurgeFrom_$(QueueName)]
AS
BEGIN
	DELETE FROM [$(QueueName)]
END
GO