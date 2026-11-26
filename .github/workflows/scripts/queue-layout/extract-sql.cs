#:project ../../../../src/NServiceBus.Transport.SqlServer/NServiceBus.Transport.SqlServer.csproj
#:property PublishAot=false
#:property Nullable=disable

// Writes the transport's SQL Server statements to files, so that the measurements run the statements of this commit.
// usage: dotnet run extract-sql.cs -- <sql dir>
var directory = Directory.CreateDirectory(args[0]).FullName;
var type = typeof(NServiceBus.SqlServerTransport).Assembly.GetType("NServiceBus.Transport.SqlServer.SqlServerConstants", true);
var constants = Activator.CreateInstance(type, true);
string Statement(string property) => (string)type.GetProperty(property).GetValue(constants);

foreach (var (file, property) in new[]
{
    ("create-heap", "CreateQueueText"),
    ("create-bucketed", "CreateBucketedQueueText"),
    ("create-delayed", "CreateDelayedMessageStoreText"),
    ("send", "SendTextWithRecoverable"),
    ("receive", "ReceiveText"),
    ("peek", "PeekText"),
    ("purge-expired", "PurgeBatchOfExpiredMessagesText"),
    ("mover", "MoveDueDelayedMessageText"),
    ("mover-lock", "MoveDueDelayedMessageWithLockText")
})
{
    File.WriteAllText(Path.Combine(directory, $"{file}.sql"), Statement(property));
}

// the bucketed table with a persisted Bucket column, for comparison only
const string bucket = "Bucket AS ISNULL(CONVERT(smallint, RowVersion % 64), 0)";
var bucketed = Statement("CreateBucketedQueueText");
if (!bucketed.Contains(bucket))
{
    throw new Exception("The Bucket column definition changed. Update this script.");
}

File.WriteAllText(Path.Combine(directory, "create-persisted.sql"), bucketed.Replace(bucket, bucket + " PERSISTED"));

// the table that #613 replaced, which clustered Index_RowVersion, with the current columns and Index_Expires; for comparison only
const string rowVersionIndex = "CREATE NONCLUSTERED INDEX Index_RowVersion ON {0}";
var heap = Statement("CreateQueueText");
if (!heap.Contains(rowVersionIndex))
{
    throw new Exception("The Index_RowVersion definition changed. Update this script.");
}

File.WriteAllText(Path.Combine(directory, "create-rowversion.sql"), heap.Replace(rowVersionIndex, "CREATE CLUSTERED INDEX Index_RowVersion ON {0}"));
