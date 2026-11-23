# Public transport options

Apply these conventions when adding a public options type or setting to the SQL Server or PostgreSQL transport. Do not change existing public signatures or defaults just to match this guideline.

- Name each type for the feature it configures and end the name with `Options`. Use `NServiceBus.Transport.SqlServer` or `NServiceBus.Transport.PostgreSql` so projects that reference both packages can distinguish types with the same name. See the [namespace correction for `FailureInfoCacheOptions` in #1838](https://github.com/Particular/NServiceBus.SqlServer/pull/1838/commits/f0e2272fb5e5befbc4d118cb27efe7a6ad38f4d4).
- Let the transport construct the options object. Give new options types an internal constructor and expose an initialized instance through a getter-only transport property. Callers configure the instance rather than replacing it.
- Give optional settings defaults that require no caller configuration. Preserve existing behavior when callers leave a new setting unset. Document the default, valid values, and the meaning of `null` where applicable in XML documentation.
- Reject invalid values when callers assign a property or call a configuration method if that value can be validated on its own. Validate rules that depend on other settings once the transport has the complete configuration.
