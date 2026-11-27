--------------------------- MODULE ReceiveBaseline ---------------------------
(***************************************************************************)
(* Message receiving without anchoring, as on master.                       *)
(*                                                                         *)
(* Each endpoint instance runs a receive loop that peeks the queue table,   *)
(* then starts up to that many concurrent receives. A receive deletes the   *)
(* lowest unlocked row (UPDLOCK/READPAST/ROWLOCK on SQL Server, FOR UPDATE  *)
(* SKIP LOCKED on PostgreSQL) inside a transaction, and the transaction     *)
(* later commits or rolls back. The next peek waits until every receive of *)
(* the batch has returned, but not until processing has finished.          *)
(*                                                                         *)
(* Sequence numbers stand for RowVersion (SQL Server) and Seq (PostgreSQL). *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Procs,            \* endpoint instances competing for one queue table
    Slots,            \* concurrency slots per instance (MaxConcurrency)
    N,                \* number of messages sent during the run
    OutOfOrderSends,  \* TRUE: a send's insert can commit after a later send's insert
    MaxRollbacks,     \* bound on processing failures
    MaxCrashes        \* bound on instance crashes

Workers == Procs \X Slots
Seqs == 1..N
Min(S) == CHOOSE x \in S : \A y \in S : x <= y
Max(S) == CHOOSE x \in S : \A y \in S : x >= y
Lesser(a, b) == IF a < b THEN a ELSE b

VARIABLES
    row,        \* row[s]: "absent", "pending" (insert not committed), "ready", "locked", "done"
    sent,       \* sequence numbers handed out to senders
    pc,         \* pc[w]: "idle", "spawned", "holding"
    got,        \* got[w]: the row a holding worker has locked
    rollbacks,
    crashes

vars == <<row, sent, pc, got, rollbacks, crashes>>

TypeOK ==
    /\ row \in [Seqs -> {"absent", "pending", "ready", "locked", "done"}]
    /\ sent \in 0..N
    /\ pc \in [Workers -> {"idle", "spawned", "holding"}]
    /\ got \in [Workers -> 0..N]
    /\ rollbacks \in 0..MaxRollbacks
    /\ crashes \in 0..MaxCrashes

SlotsOf(p) == {w \in Workers : w[1] = p}
Ready == {s \in Seqs : row[s] = "ready"}

Init ==
    /\ row = [s \in Seqs |-> "absent"]
    /\ sent = 0
    /\ pc = [w \in Workers |-> "idle"]
    /\ got = [w \in Workers |-> 0]
    /\ rollbacks = 0
    /\ crashes = 0

Send ==
    /\ sent < N
    /\ sent' = sent + 1
    /\ row' = [row EXCEPT ![sent + 1] = IF OutOfOrderSends THEN "pending" ELSE "ready"]
    /\ UNCHANGED <<pc, got, rollbacks, crashes>>

CommitSend(s) ==
    /\ row[s] = "pending"
    /\ row' = [row EXCEPT ![s] = "ready"]
    /\ UNCHANGED <<sent, pc, got, rollbacks, crashes>>

\* The peek estimates max - min + 1 over unlocked rows, so locked rows between
\* them are counted too; the loop starts at most that many receives.
Peek(p) ==
    LET idle == {w \in SlotsOf(p) : pc[w] = "idle"}
        count == IF Ready = {} THEN 0 ELSE Max(Ready) - Min(Ready) + 1
    IN
    /\ \A w \in SlotsOf(p) : pc[w] \in {"idle", "holding"}
    /\ idle # {}
    /\ count > 0
    /\ \E spawn \in SUBSET idle :
         /\ Cardinality(spawn) = Lesser(count, Cardinality(idle))
         /\ pc' = [w \in Workers |-> IF w \in spawn THEN "spawned" ELSE pc[w]]
    /\ UNCHANGED <<row, sent, got, rollbacks, crashes>>

\* DELETE TOP(1) ... ORDER BY RowVersion, skipping locked rows
Receive(w) ==
    /\ pc[w] = "spawned"
    /\ IF Ready = {}
       THEN /\ pc' = [pc EXCEPT ![w] = "idle"]
            /\ UNCHANGED <<row, got>>
       ELSE /\ row' = [row EXCEPT ![Min(Ready)] = "locked"]
            /\ got' = [got EXCEPT ![w] = Min(Ready)]
            /\ pc' = [pc EXCEPT ![w] = "holding"]
    /\ UNCHANGED <<sent, rollbacks, crashes>>

Commit(w) ==
    /\ pc[w] = "holding"
    /\ row' = [row EXCEPT ![got[w]] = "done"]
    /\ pc' = [pc EXCEPT ![w] = "idle"]
    /\ got' = [got EXCEPT ![w] = 0]
    /\ UNCHANGED <<sent, rollbacks, crashes>>

Rollback(w) ==
    /\ pc[w] = "holding"
    /\ rollbacks < MaxRollbacks
    /\ rollbacks' = rollbacks + 1
    /\ row' = [row EXCEPT ![got[w]] = "ready"]
    /\ pc' = [pc EXCEPT ![w] = "idle"]
    /\ got' = [got EXCEPT ![w] = 0]
    /\ UNCHANGED <<sent, crashes>>

\* The database rolls back the open transactions of a crashed instance.
Crash(p) ==
    /\ crashes < MaxCrashes
    /\ crashes' = crashes + 1
    /\ row' = [s \in Seqs |->
                 IF \E w \in SlotsOf(p) : pc[w] = "holding" /\ got[w] = s
                 THEN "ready" ELSE row[s]]
    /\ pc' = [w \in Workers |-> IF w[1] = p THEN "idle" ELSE pc[w]]
    /\ got' = [w \in Workers |-> IF w[1] = p THEN 0 ELSE got[w]]
    /\ UNCHANGED <<sent, rollbacks>>

Next ==
    \/ Send
    \/ \E s \in Seqs : CommitSend(s)
    \/ \E p \in Procs : Peek(p) \/ Crash(p)
    \/ \E w \in Workers : Receive(w) \/ Commit(w) \/ Rollback(w)

Fairness ==
    /\ WF_vars(Send)
    /\ \A s \in Seqs : WF_vars(CommitSend(s))
    /\ \A p \in Procs : WF_vars(Peek(p))
    /\ \A w \in Workers : WF_vars(Receive(w)) /\ WF_vars(Commit(w))

Spec == Init /\ [][Next]_vars /\ Fairness

\* Two receivers never hold the same row.
ExclusiveLocks ==
    \A w1, w2 \in Workers :
        (w1 # w2 /\ pc[w1] = "holding" /\ pc[w2] = "holding") => got[w1] # got[w2]

Done == \A s \in Seqs : row[s] = "done"

\* Every sent message is eventually processed.
AllDelivered == <>Done

=============================================================================
