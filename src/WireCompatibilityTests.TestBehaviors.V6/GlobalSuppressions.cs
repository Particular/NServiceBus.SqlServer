// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Code", "PS0013:A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext", Justification = "<Pending>", Scope = "member", Target = "~M:Discard.Invoke(NServiceBus.Pipeline.IIncomingPhysicalMessageContext,System.Func{NServiceBus.Pipeline.IIncomingPhysicalMessageContext,System.Threading.Tasks.Task})~System.Threading.Tasks.Task")]
