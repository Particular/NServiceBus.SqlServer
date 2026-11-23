# Public transport options namespaces

This guideline applies when adding a public options type to the SQL Server or PostgreSQL transport.

Declare the type in `NServiceBus.Transport.SqlServer` or `NServiceBus.Transport.PostgreSql`, according to the transport that exposes it. Leave existing public options types in their current namespaces to preserve compatibility.

Separate namespaces let projects that reference both packages distinguish options types with the same name. See the [namespace correction for `FailureInfoCacheOptions` in #1838](https://github.com/Particular/NServiceBus.SqlServer/pull/1838/commits/f0e2272fb5e5befbc4d118cb27efe7a6ad38f4d4).
