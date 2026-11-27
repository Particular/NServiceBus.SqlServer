------------------------- MODULE ReceiveSingleAnchor -------------------------
(***************************************************************************)
(* Receiving with one anchor and an epoch per instance. Only the peek and  *)
(* a rollback write the cursor; receives seek from the anchor they read    *)
(* and write nothing.                                                       *)
(*                                                                         *)
(* The peek is two steps, so a rollback can run between them: reading      *)
(* captures (anchor, epoch) and queries the queue, applying sets the       *)
(* anchor to just before the lowest ready row found. A rollback of row s   *)
(* sets anchor := min(anchor, s - 1) and increments the epoch.             *)
(*                                                                         *)
(* EpochGuard: TRUE applies a peek result only if the epoch is unchanged   *)
(*   since the peek read the anchor.                                       *)
(* DroppedApply, what a peek result whose epoch changed does:              *)
(*   "Drop"  - nothing                                                     *)
(*   "Lower" - anchor := min(anchor, lowest - 1)                           *)
(* HeadEvery: every HeadEvery-th peek seeks from the start; 0 for never.   *)
(* HeadAfterEmpty: TRUE seeks from the start after a batch that received   *)
(*   no row.                                                               *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Procs,             \* endpoint instances competing for one queue table
    Slots,             \* concurrency slots per instance (MaxConcurrency)
    N,                 \* number of messages sent during the run
    OutOfOrderSends,   \* TRUE: a send's insert can commit after a later send's insert
    EpochGuard,
    DroppedApply,
    HeadEvery,
    HeadAfterEmpty,
    MaxRollbacks,      \* bound on processing failures
    MaxReceiveFaults,  \* bound on receives that lock a row, then fail
    MaxCrashes,        \* bound on instance crashes
    CrashRestarts      \* TRUE: a crashed instance restarts; FALSE: it stays down (scale-in)

ASSUME DroppedApply \in {"Drop", "Lower"}
ASSUME HeadEvery \in Nat

Workers == Procs \X Slots
Seqs == 1..N
Min(S) == CHOOSE x \in S : \A y \in S : x <= y
Max(S) == CHOOSE x \in S : \A y \in S : x >= y
Lesser(a, b) == IF a < b THEN a ELSE b

VARIABLES
    row,        \* row[s]: "absent", "pending" (insert not committed), "ready", "locked", "done"
    sent,       \* sequence numbers handed out to senders
    anchor,     \* anchor[p]
    epoch,      \* epoch[p]
    peekNo,     \* peekNo[p]: peeks since the last scheduled head peek
    hadRows,    \* hadRows[p]: a receive got a row since the last peek
    peek,       \* peek[p]: the peek between its read and its apply
    pc,         \* pc[w]: "idle", "spawned", "query", "holding", "down"
    used,       \* used[w]: the anchor a receive read
    got,        \* got[w]: the row a receive holds, 0 for none
    rollbacks,
    faults,
    crashes

vars == <<row, sent, anchor, epoch, peekNo, hadRows, peek, pc, used, got, rollbacks, faults, crashes>>

NoPeek == [busy |-> FALSE, epoch |-> 0, lowest |-> 0, count |-> 0]

TypeOK ==
    /\ row \in [Seqs -> {"absent", "pending", "ready", "locked", "done"}]
    /\ sent \in 0..N
    /\ anchor \in [Procs -> 0..N]
    /\ epoch \in [Procs -> 0..MaxRollbacks]
    /\ hadRows \in [Procs -> BOOLEAN]
    /\ peek \in [Procs -> [busy : BOOLEAN, epoch : 0..MaxRollbacks, lowest : 0..N, count : 0..N]]
    /\ pc \in [Workers -> {"idle", "spawned", "query", "holding", "down"}]
    /\ used \in [Workers -> 0..N]
    /\ got \in [Workers -> 0..N]

SlotsOf(p) == {w \in Workers : w[1] = p}
Up(p) == \A w \in SlotsOf(p) : pc[w] # "down"
ReadyAfter(a) == {s \in Seqs : s > a /\ row[s] = "ready"}

Init ==
    /\ row = [s \in Seqs |-> "absent"]
    /\ sent = 0
    /\ anchor = [p \in Procs |-> 0]
    /\ epoch = [p \in Procs |-> 0]
    /\ peekNo = [p \in Procs |-> 0]
    /\ hadRows = [p \in Procs |-> TRUE]
    /\ peek = [p \in Procs |-> NoPeek]
    /\ pc = [w \in Workers |-> "idle"]
    /\ used = [w \in Workers |-> 0]
    /\ got = [w \in Workers |-> 0]
    /\ rollbacks = 0
    /\ faults = 0
    /\ crashes = 0

Send ==
    /\ sent < N
    /\ sent' = sent + 1
    /\ row' = [row EXCEPT ![sent + 1] = IF OutOfOrderSends THEN "pending" ELSE "ready"]
    /\ UNCHANGED <<anchor, epoch, peekNo, hadRows, peek, pc, used, got, rollbacks, faults, crashes>>

CommitSend(s) ==
    /\ row[s] = "pending"
    /\ row' = [row EXCEPT ![s] = "ready"]
    /\ UNCHANGED <<sent, anchor, epoch, peekNo, hadRows, peek, pc, used, got, rollbacks, faults, crashes>>

\* The latch: a peek starts only after every receive of the batch has returned.
PeekRead(p) ==
    LET head == \/ HeadEvery > 0 /\ peekNo[p] = HeadEvery - 1
                \/ HeadAfterEmpty /\ ~hadRows[p]
        visible == ReadyAfter(IF head THEN 0 ELSE anchor[p])
    IN
    /\ Up(p)
    /\ ~peek[p].busy
    /\ \A w \in SlotsOf(p) : pc[w] \in {"idle", "holding"}
    /\ \E w \in SlotsOf(p) : pc[w] = "idle"
    /\ peek' = [peek EXCEPT ![p] =
                  [busy |-> TRUE, epoch |-> epoch[p],
                   lowest |-> IF visible = {} THEN 0 ELSE Min(visible),
                   count |-> IF visible = {} THEN 0 ELSE Max(visible) - Min(visible) + 1]]
    /\ peekNo' = [peekNo EXCEPT ![p] = IF HeadEvery > 0 THEN (@ + 1) % HeadEvery ELSE 0]
    /\ hadRows' = [hadRows EXCEPT ![p] = FALSE]
    /\ UNCHANGED <<row, sent, anchor, epoch, pc, used, got, rollbacks, faults, crashes>>

PeekApply(p) ==
    LET r == peek[p]
        idle == {w \in SlotsOf(p) : pc[w] = "idle"}
        current == ~EpochGuard \/ epoch[p] = r.epoch
    IN
    /\ r.busy
    /\ anchor' = [anchor EXCEPT ![p] =
                    IF r.count = 0 THEN @
                    ELSE IF current THEN r.lowest - 1
                    ELSE IF DroppedApply = "Lower" THEN Lesser(@, r.lowest - 1)
                    ELSE @]
    /\ \E spawn \in SUBSET idle :
         /\ Cardinality(spawn) = Lesser(r.count, Cardinality(idle))
         /\ pc' = [w \in Workers |-> IF w \in spawn THEN "spawned" ELSE pc[w]]
    /\ peek' = [peek EXCEPT ![p] = NoPeek]
    /\ UNCHANGED <<row, sent, epoch, peekNo, hadRows, used, got, rollbacks, faults, crashes>>

ReadAnchor(w) ==
    /\ pc[w] = "spawned"
    /\ used' = [used EXCEPT ![w] = anchor[w[1]]]
    /\ pc' = [pc EXCEPT ![w] = "query"]
    /\ UNCHANGED <<row, sent, anchor, epoch, peekNo, hadRows, peek, got, rollbacks, faults, crashes>>

\* DELETE the lowest unlocked ready row above the anchor the receive read.
DbReceive(w) ==
    LET p == w[1]
        candidates == ReadyAfter(used[w])
    IN
    /\ pc[w] = "query"
    /\ IF candidates = {}
       THEN /\ pc' = [pc EXCEPT ![w] = "idle"]
            /\ UNCHANGED <<row, got, hadRows>>
       ELSE /\ row' = [row EXCEPT ![Min(candidates)] = "locked"]
            /\ got' = [got EXCEPT ![w] = Min(candidates)]
            /\ pc' = [pc EXCEPT ![w] = "holding"]
            /\ hadRows' = [hadRows EXCEPT ![p] = TRUE]
    /\ UNCHANGED <<sent, anchor, epoch, peekNo, peek, used, rollbacks, faults, crashes>>

\* The receive locks a row and fails before it returns it, so the row is
\* released and the receive returns empty-handed.
ReceiveFault(w) ==
    /\ pc[w] = "query"
    /\ ReadyAfter(used[w]) # {}
    /\ faults < MaxReceiveFaults
    /\ faults' = faults + 1
    /\ pc' = [pc EXCEPT ![w] = "idle"]
    /\ UNCHANGED <<row, sent, anchor, epoch, peekNo, hadRows, peek, used, got, rollbacks, crashes>>

Commit(w) ==
    /\ pc[w] = "holding"
    /\ row' = [row EXCEPT ![got[w]] = "done"]
    /\ pc' = [pc EXCEPT ![w] = "idle"]
    /\ got' = [got EXCEPT ![w] = 0]
    /\ UNCHANGED <<sent, anchor, epoch, peekNo, hadRows, peek, used, rollbacks, faults, crashes>>

Rollback(w) ==
    LET p == w[1]
        s == got[w]
    IN
    /\ pc[w] = "holding"
    /\ rollbacks < MaxRollbacks
    /\ rollbacks' = rollbacks + 1
    /\ row' = [row EXCEPT ![s] = "ready"]
    /\ pc' = [pc EXCEPT ![w] = "idle"]
    /\ got' = [got EXCEPT ![w] = 0]
    /\ anchor' = [anchor EXCEPT ![p] = Lesser(@, s - 1)]
    /\ epoch' = [epoch EXCEPT ![p] = @ + 1]
    /\ UNCHANGED <<sent, peekNo, hadRows, peek, used, faults, crashes>>

\* The database releases the crashed instance's locks; a restarted instance
\* starts with (anchor, epoch) = (0, 0).
Crash(p) ==
    /\ crashes < MaxCrashes
    /\ Up(p)
    /\ crashes' = crashes + 1
    /\ row' = [s \in Seqs |->
                 IF \E w \in SlotsOf(p) : pc[w] = "holding" /\ got[w] = s
                 THEN "ready" ELSE row[s]]
    /\ anchor' = [anchor EXCEPT ![p] = 0]
    /\ epoch' = [epoch EXCEPT ![p] = 0]
    /\ peekNo' = [peekNo EXCEPT ![p] = 0]
    /\ hadRows' = [hadRows EXCEPT ![p] = TRUE]
    /\ peek' = [peek EXCEPT ![p] = NoPeek]
    /\ pc' = [w \in Workers |->
                IF w[1] = p THEN (IF CrashRestarts THEN "idle" ELSE "down") ELSE pc[w]]
    /\ used' = [w \in Workers |-> IF w[1] = p THEN 0 ELSE used[w]]
    /\ got' = [w \in Workers |-> IF w[1] = p THEN 0 ELSE got[w]]
    /\ UNCHANGED <<sent, rollbacks, faults>>

Next ==
    \/ Send
    \/ \E s \in Seqs : CommitSend(s)
    \/ \E p \in Procs : PeekRead(p) \/ PeekApply(p) \/ Crash(p)
    \/ \E w \in Workers :
          ReadAnchor(w) \/ DbReceive(w) \/ ReceiveFault(w) \/ Commit(w) \/ Rollback(w)

\* Failures and crashes are adversarial and get no fairness.
Fairness ==
    /\ WF_vars(Send)
    /\ \A s \in Seqs : WF_vars(CommitSend(s))
    /\ \A p \in Procs : WF_vars(PeekRead(p)) /\ WF_vars(PeekApply(p))
    /\ \A w \in Workers :
          WF_vars(ReadAnchor(w)) /\ WF_vars(DbReceive(w)) /\ WF_vars(Commit(w))

Spec == Init /\ [][Next]_vars /\ Fairness

\* Two receivers never hold the same row.
ExclusiveLocks ==
    \A w1, w2 \in Workers :
        (w1 # w2 /\ pc[w1] = "holding" /\ pc[w2] = "holding") => got[w1] # got[w2]

\* The cursor alone never strands a row: some running instance still seeks
\* from before every ready row. Out-of-order commits and instances that stay
\* down break it by design; only a peek from the head recovers those.
NoStrandedRow ==
    \A s \in Seqs : row[s] = "ready" => \E p \in Procs : Up(p) /\ s > anchor[p]

Done == \A s \in Seqs : row[s] = "done"

\* Every sent message is eventually processed.
AllDelivered == <>Done

=============================================================================
