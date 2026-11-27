--------------------------- MODULE ReceiveAnchored ---------------------------
(***************************************************************************)
(* Anchored message receiving, as on the anchored-read branch, with the     *)
(* anchor-discarding strategy as a parameter.                               *)
(*                                                                         *)
(* Each instance keeps a ReceiveCursor with a high-water anchor (hw) and a  *)
(* sweep anchor (sweep). A receive seeks past the sweep anchor while one is *)
(* set, and past hw otherwise. A receive is split into the steps the code   *)
(* performs separately, so other receives can interleave between them:     *)
(*   ReadAnchor  - ReceiveCursor.GetAnchor                                  *)
(*   DbReceive   - the DELETE ... WHERE RowVersion > @Anchor statement       *)
(*   Received    - ReceiveCursor.Received                                   *)
(*   Commit / Rollback - the end of the transaction, then Settle            *)
(* Each ReceiveCursor method is modeled as one atomic step; its internal    *)
(* compare-exchange loops are not split further.                            *)
(*                                                                         *)
(* RollbackPolicy, what a rolled-back receive does to the cursor:           *)
(*   "None"        - nothing; rely on the peek and the head sweep           *)
(*   "Reset"       - discard both anchors (hw := 0, no sweep)               *)
(*   "Rescan"      - RescanFrom(row), as implemented on the branch           *)
(*   "RescanGuarded" - RescanFrom(row), and a sweep receive moves the sweep *)
(*                   past a row only if the sweep is not behind the anchor *)
(*                   the receive read (anchored-read-sweep-rollback-test)  *)
(*   "RescanEpoch" - RescanFrom(row), and a receive that read its anchor     *)
(*                   before a sweep was started or lowered does not move or  *)
(*                   end the sweep                                          *)
(*                                                                         *)
(* PeekFrom, where the peek seeks from:                                     *)
(*   "Head"              - always from the start (EnablePeekAnchoring off)  *)
(*   "HighWaterIdleHead" - from hw, from the start after an empty batch     *)
(*                         (EnablePeekAnchoring on, as implemented)          *)
(*   "Cursor"            - from the receive anchor, never from the start;   *)
(*                         isolates the cursor from every backstop          *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Procs,                 \* endpoint instances competing for one queue table
    Slots,                 \* concurrency slots per instance (MaxConcurrency)
    N,                     \* number of messages sent during the run
    OutOfOrderSends,       \* TRUE: a send's insert can commit after a later send's insert
    RollbackPolicy,
    RescanOnReceiveFault,  \* TRUE: a receive that fails before it learns its row sweeps from its anchor
    PeekFrom,
    HeadSweep,             \* TRUE: HeadSweepInterval elapses at arbitrary times
    MaxRollbacks,          \* bound on processing failures
    MaxReceiveFaults,      \* bound on receives that lock a row, then fail before ReceiveCursor.Received
    MaxCrashes,            \* bound on instance crashes
    CrashRestarts          \* TRUE: a crashed instance restarts; FALSE: it stays down (scale-in)

ASSUME RollbackPolicy \in {"None", "Reset", "Rescan", "RescanGuarded", "RescanEpoch"}
ASSUME PeekFrom \in {"Head", "HighWaterIdleHead", "Cursor"}

Workers == Procs \X Slots
Seqs == 1..N
NoSweep == N + 1
Min(S) == CHOOSE x \in S : \A y \in S : x <= y
Max(S) == CHOOSE x \in S : \A y \in S : x >= y
Lesser(a, b) == IF a < b THEN a ELSE b
Greater(a, b) == IF a > b THEN a ELSE b

VARIABLES
    row,        \* row[s]: "absent", "pending" (insert not committed), "ready", "locked", "done"
    sent,       \* sequence numbers handed out to senders
    hw,         \* hw[p]: high-water anchor of instance p
    sweep,      \* sweep[p]: sweep anchor of instance p, NoSweep when no sweep is running
    hadRows,    \* hadRows[p]: BatchActivity, whether a receive got a row since the last peek
    pc,         \* pc[w]: "idle", "spawned", "query", "cursor", "holding"
    used,       \* used[w]: the anchor a receive read
    got,        \* got[w]: the row a receive locked, 0 for none
    stale,      \* stale[w]: a sweep was started or lowered after the receive read its anchor
    rollbacks,
    faults,
    crashes

vars == <<row, sent, hw, sweep, hadRows, pc, used, got, stale, rollbacks, faults, crashes>>

NoAnchor == [seek |-> 0, kind |-> "HighWater"]

TypeOK ==
    /\ row \in [Seqs -> {"absent", "pending", "ready", "locked", "done"}]
    /\ sent \in 0..N
    /\ hw \in [Procs -> 0..N]
    /\ sweep \in [Procs -> 0..NoSweep]
    /\ hadRows \in [Procs -> BOOLEAN]
    /\ pc \in [Workers -> {"idle", "spawned", "query", "cursor", "holding", "down"}]
    /\ used \in [Workers -> [seek : 0..N, kind : {"HighWater", "Sweep"}]]
    /\ got \in [Workers -> 0..N]
    /\ stale \in [Workers -> BOOLEAN]
    /\ rollbacks \in 0..MaxRollbacks
    /\ faults \in 0..MaxReceiveFaults
    /\ crashes \in 0..MaxCrashes

SlotsOf(p) == {w \in Workers : w[1] = p}
InFlight(p) == {w \in SlotsOf(p) : pc[w] \in {"query", "cursor"}}
ReadyAfter(a) == {s \in Seqs : s > a /\ row[s] = "ready"}

\* ReceiveCursor.GetAnchor
CurrentAnchor(p) ==
    IF sweep[p] # NoSweep
    THEN [seek |-> sweep[p], kind |-> "Sweep"]
    ELSE [seek |-> hw[p], kind |-> "HighWater"]

\* ReceiveCursor.StartOrLowerSweep. Marks receives under way as stale, which
\* only RollbackPolicy "RescanEpoch" reads.
StartOrLowerSweep(p, a) ==
    /\ sweep' = [sweep EXCEPT ![p] = Lesser(@, a)]
    /\ stale' = [w \in Workers |-> IF w \in InFlight(p) THEN TRUE ELSE stale[w]]

Init ==
    /\ row = [s \in Seqs |-> "absent"]
    /\ sent = 0
    /\ hw = [p \in Procs |-> 0]
    /\ sweep = [p \in Procs |-> NoSweep]
    /\ hadRows = [p \in Procs |-> TRUE]
    /\ pc = [w \in Workers |-> "idle"]
    /\ used = [w \in Workers |-> NoAnchor]
    /\ got = [w \in Workers |-> 0]
    /\ stale = [w \in Workers |-> FALSE]
    /\ rollbacks = 0
    /\ faults = 0
    /\ crashes = 0

Send ==
    /\ sent < N
    /\ sent' = sent + 1
    /\ row' = [row EXCEPT ![sent + 1] = IF OutOfOrderSends THEN "pending" ELSE "ready"]
    /\ UNCHANGED <<hw, sweep, hadRows, pc, used, got, stale, rollbacks, faults, crashes>>

CommitSend(s) ==
    /\ row[s] = "pending"
    /\ row' = [row EXCEPT ![s] = "ready"]
    /\ UNCHANGED <<sent, hw, sweep, hadRows, pc, used, got, stale, rollbacks, faults, crashes>>

PeekSeekAfter(p) ==
    CASE PeekFrom = "Head" -> 0
      [] PeekFrom = "HighWaterIdleHead" -> IF hadRows[p] THEN hw[p] ELSE 0
      [] PeekFrom = "Cursor" -> CurrentAnchor(p).seek

\* MessageReceiver.ReceiveMessages: BatchActivity.BeginNextBatch, the peek,
\* ReceiveCursor.ApplyPeekResult, then starting the receives. The latch makes
\* the next peek wait until every receive of the batch has returned.
Peek(p) ==
    LET idle == {w \in SlotsOf(p) : pc[w] = "idle"}
        visible == ReadyAfter(PeekSeekAfter(p))
        count == IF visible = {} THEN 0 ELSE Max(visible) - Min(visible) + 1
        seekAfter == Min(visible) - 1
    IN
    /\ \A w \in SlotsOf(p) : pc[w] \in {"idle", "holding"}
    /\ idle # {}
    /\ hadRows' = [hadRows EXCEPT ![p] = FALSE]
    /\ IF count = 0
       THEN UNCHANGED <<hw, sweep, stale, pc>>
       ELSE /\ IF seekAfter < hw[p]
               THEN StartOrLowerSweep(p, seekAfter) /\ UNCHANGED hw
               ELSE /\ hw' = [hw EXCEPT ![p] = Greater(@, seekAfter)]
                    /\ UNCHANGED <<sweep, stale>>
            /\ \E spawn \in SUBSET idle :
                 /\ Cardinality(spawn) = Lesser(count, Cardinality(idle))
                 /\ pc' = [w \in Workers |-> IF w \in spawn THEN "spawned" ELSE pc[w]]
    /\ UNCHANGED <<row, sent, used, got, rollbacks, faults, crashes>>

\* HeadSweepSchedule.SweepIfDue -> ReceiveCursor.SweepFromHead
HeadSweepStep(p) ==
    /\ HeadSweep
    /\ hw[p] > 0
    /\ StartOrLowerSweep(p, 0)
    /\ UNCHANGED <<row, sent, hw, hadRows, pc, used, got, rollbacks, faults, crashes>>

ReadAnchor(w) ==
    /\ pc[w] = "spawned"
    /\ used' = [used EXCEPT ![w] = CurrentAnchor(w[1])]
    /\ stale' = [stale EXCEPT ![w] = FALSE]
    /\ pc' = [pc EXCEPT ![w] = "query"]
    /\ UNCHANGED <<row, sent, hw, sweep, hadRows, got, rollbacks, faults, crashes>>

\* DELETE TOP(1) ... WHERE RowVersion > @Anchor ORDER BY RowVersion, skipping locked rows
DbReceive(w) ==
    LET candidates == ReadyAfter(used[w].seek) IN
    /\ pc[w] = "query"
    /\ IF candidates = {}
       THEN UNCHANGED <<row, got>>
       ELSE /\ row' = [row EXCEPT ![Min(candidates)] = "locked"]
            /\ got' = [got EXCEPT ![w] = Min(candidates)]
    /\ pc' = [pc EXCEPT ![w] = "cursor"]
    /\ UNCHANGED <<sent, hw, sweep, hadRows, used, stale, rollbacks, faults, crashes>>

\* ReceiveCursor.Received
Received(w) ==
    LET p == w[1]
        a == used[w]
        s == got[w]
        keepSweep == RollbackPolicy = "RescanEpoch" /\ stale[w]
    IN
    /\ pc[w] = "cursor"
    /\ IF a.kind = "Sweep" /\ s # 0 /\ s < hw[p]
       THEN \* MoveSweepPast
            /\ sweep' = [sweep EXCEPT ![p] =
                           IF \/ keepSweep \/ @ = NoSweep \/ s <= @
                              \/ RollbackPolicy = "RescanGuarded" /\ @ < a.seek
                           THEN @ ELSE s]
            /\ UNCHANGED hw
       ELSE \* end the sweep if it has not moved since this receive read it, then AdvanceAnchor
            /\ sweep' = [sweep EXCEPT ![p] =
                           IF a.kind = "Sweep" /\ ~keepSweep /\ @ = a.seek THEN NoSweep ELSE @]
            /\ hw' = [hw EXCEPT ![p] = Greater(@, s)]
    /\ hadRows' = IF s # 0 THEN [hadRows EXCEPT ![p] = TRUE] ELSE hadRows
    /\ pc' = [pc EXCEPT ![w] = IF s = 0 THEN "idle" ELSE "holding"]
    /\ used' = [used EXCEPT ![w] = NoAnchor]
    /\ stale' = [stale EXCEPT ![w] = FALSE]
    /\ UNCHANGED <<row, sent, got, rollbacks, faults, crashes>>

\* TryReceive locks a row, then throws before the row version reaches the cursor
\* (for example, the connection drops while the body is read).
ReceiveFault(w) ==
    /\ pc[w] = "cursor"
    /\ got[w] # 0
    /\ faults < MaxReceiveFaults
    /\ faults' = faults + 1
    /\ row' = [row EXCEPT ![got[w]] = "ready"]
    /\ pc' = [pc EXCEPT ![w] = "idle"]
    /\ got' = [got EXCEPT ![w] = 0]
    /\ used' = [used EXCEPT ![w] = NoAnchor]
    /\ IF RescanOnReceiveFault
       THEN StartOrLowerSweep(w[1], used[w].seek)
       ELSE UNCHANGED <<sweep, stale>>
    /\ UNCHANGED <<sent, hw, hadRows, rollbacks, crashes>>

Commit(w) ==
    /\ pc[w] = "holding"
    /\ row' = [row EXCEPT ![got[w]] = "done"]
    /\ pc' = [pc EXCEPT ![w] = "idle"]
    /\ got' = [got EXCEPT ![w] = 0]
    /\ UNCHANGED <<sent, hw, sweep, hadRows, used, stale, rollbacks, faults, crashes>>

\* The transaction rolls back, then ReceiveAttempt.Settle applies the policy.
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
    /\ CASE RollbackPolicy = "None" ->
              UNCHANGED <<hw, sweep, stale>>
         [] RollbackPolicy = "Reset" ->
              /\ hw' = [hw EXCEPT ![p] = 0]
              /\ sweep' = [sweep EXCEPT ![p] = NoSweep]
              /\ UNCHANGED stale
         [] RollbackPolicy \in {"Rescan", "RescanGuarded", "RescanEpoch"} ->
              /\ IF s - 1 < hw[p]
                 THEN StartOrLowerSweep(p, s - 1)
                 ELSE UNCHANGED <<sweep, stale>>
              /\ UNCHANGED hw
    /\ UNCHANGED <<sent, hadRows, used, faults, crashes>>

\* The database rolls back the crashed instance's open transactions; a
\* restarted instance starts with a new cursor.
Crash(p) ==
    /\ crashes < MaxCrashes
    /\ \A w \in SlotsOf(p) : pc[w] # "down"
    /\ crashes' = crashes + 1
    /\ row' = [s \in Seqs |->
                 IF \E w \in SlotsOf(p) : pc[w] \in {"cursor", "holding"} /\ got[w] = s
                 THEN "ready" ELSE row[s]]
    /\ hw' = [hw EXCEPT ![p] = 0]
    /\ sweep' = [sweep EXCEPT ![p] = NoSweep]
    /\ hadRows' = [hadRows EXCEPT ![p] = TRUE]
    /\ pc' = [w \in Workers |->
                IF w[1] = p THEN (IF CrashRestarts THEN "idle" ELSE "down") ELSE pc[w]]
    /\ used' = [w \in Workers |-> IF w[1] = p THEN NoAnchor ELSE used[w]]
    /\ got' = [w \in Workers |-> IF w[1] = p THEN 0 ELSE got[w]]
    /\ stale' = [w \in Workers |-> IF w[1] = p THEN FALSE ELSE stale[w]]
    /\ UNCHANGED <<sent, rollbacks, faults>>

Next ==
    \/ Send
    \/ \E s \in Seqs : CommitSend(s)
    \/ \E p \in Procs : Peek(p) \/ HeadSweepStep(p) \/ Crash(p)
    \/ \E w \in Workers :
          \/ ReadAnchor(w) \/ DbReceive(w) \/ Received(w) \/ ReceiveFault(w)
          \/ Commit(w) \/ Rollback(w)

\* Failures and crashes are adversarial and get no fairness.
Fairness ==
    /\ WF_vars(Send)
    /\ \A s \in Seqs : WF_vars(CommitSend(s))
    /\ \A p \in Procs : WF_vars(Peek(p)) /\ WF_vars(HeadSweepStep(p))
    /\ \A w \in Workers :
          /\ WF_vars(ReadAnchor(w)) /\ WF_vars(DbReceive(w))
          /\ WF_vars(Received(w)) /\ WF_vars(Commit(w))

Spec == Init /\ [][Next]_vars /\ Fairness

\* Two receivers never hold the same row.
ExclusiveLocks ==
    \A w1, w2 \in Workers :
        (w1 # w2 /\ pc[w1] \in {"cursor", "holding"} /\ pc[w2] \in {"cursor", "holding"}
            /\ got[w1] # 0)
        => got[w1] # got[w2]

\* A sweep anchor never sits past the high-water anchor.
SweepBehindHighWater ==
    \A p \in Procs : sweep[p] = NoSweep \/ sweep[p] <= hw[p]

\* The cursor alone never strands a row: some instance still seeks from before
\* every ready row. Out-of-order commits and instances that stay down break it
\* by design; only the peek from the head and the head sweep recover those.
NoStrandedRow ==
    \A s \in Seqs : row[s] = "ready" => \E p \in Procs : s > CurrentAnchor(p).seek

Done == \A s \in Seqs : row[s] = "done"

\* Every sent message is eventually processed.
AllDelivered == <>Done

=============================================================================
