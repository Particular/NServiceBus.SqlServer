# Receive models

TLA+ models of how competing receivers take rows from a queue table.

- `ReceiveBaseline.tla`: receiving without anchoring, as on `master`. Each receive deletes the lowest unlocked row.
- `ReceiveAnchored.tla`: receiving with a `ReceiveCursor`, as on `anchored-read` ([#1847](https://github.com/Particular/NServiceBus.SqlServer/pull/1847)). Each `ReceiveCursor` method is one atomic step. A receive is split into reading the anchor, the `DELETE`, and updating the cursor, so other receives can interleave between those steps.

- `ReceiveSingleAnchor.tla`: an alternative design with one anchor and an epoch per instance. Only the peek and a rollback write the cursor; receives seek from the anchor they read and write nothing. The peek reads the anchor and the epoch, queries the queue, and then sets the anchor to just before the lowest ready row it found. A rollback of row `s` sets the anchor to `min(anchor, s - 1)` and increments the epoch.

`ReceiveAnchored.tla` takes the anchor-discarding strategy as constants:

- `RollbackPolicy`: what a rolled-back receive does to the cursor.
  - `None`: nothing.
  - `Reset`: discard both anchors.
  - `Rescan`: `ReceiveCursor.RescanFrom`, as on `anchored-read`.
  - `RescanGuarded`: `RescanFrom`, and a sweep receive moves the sweep past its row only if the sweep is not behind the anchor the receive read, as in [#1848](https://github.com/Particular/NServiceBus.SqlServer/pull/1848).
  - `RescanEpoch`: `RescanFrom`, and a receive that read its anchor before a sweep was started or lowered does not move or end the sweep, as in [#1858](https://github.com/Particular/NServiceBus.SqlServer/pull/1858).
- `RescanOnReceiveFault`: a receive that locks a row and then fails before the cursor learns the row version starts a sweep from the anchor it used.
- `PeekFrom`: `Head` (`EnablePeekAnchoring` off), `HighWaterIdleHead` (`EnablePeekAnchoring` on, as implemented), or `Cursor`. `Cursor` never peeks from the head, so it isolates the cursor from the peek backstop.
- `HeadSweep`: whether `HeadSweepInterval` elapses.

`ReceiveSingleAnchor.tla` takes these constants:

- `EpochGuard`: the peek applies its result only if the epoch has not changed since it read the anchor.
- `DroppedApply`: what a peek does with a result whose epoch changed. `Drop` discards it; `Lower` sets the anchor to `min(anchor, lowest - 1)`.
- `HeadEvery`: every `HeadEvery`-th peek seeks from the start of the queue; `0` for never.
- `HeadAfterEmpty`: a peek after a batch that received no row seeks from the start of the queue.

All specs check `ExclusiveLocks` and the liveness property `AllDelivered`: every sent message is eventually processed. Rollbacks, receive faults, and crashes are bounded and get no fairness.

`ReceiveAnchored.tla` and `ReceiveSingleAnchor.tla` also define the invariant `NoStrandedRow`: some instance still seeks from before every ready row. It fails at the step that strands a row, and checking it is much cheaper than checking `AllDelivered`. It applies only to configurations without out-of-order commits and without instances that stay down, because those strand rows by design and only the peek from the head and the head sweep recover them.

## Running

Use TLC from [tla2tools.jar](https://github.com/tlaplus/tlaplus/releases) 1.7 or later, which needs Java 11 or later. The TLA+ VS Code extension includes a copy of the jar. `tla2tools.jar` and TLC's `states/` folders are ignored in this folder.

```
java -Xmx16g -cp tla2tools.jar tlc2.TLC -workers auto -config ReceiveAnchored_Rescan_NoBackstop.cfg ReceiveAnchored.tla
```

## Configurations

Configurations use three messages (`N = 3`) unless their name gives another count. `NoBackstop` and `RescanEpoch_N*` configurations peek from the cursor and never sweep from the head, so they test the cursor alone. In `ReceiveSingleAnchor` configurations, `NoBackstop` means that no peek seeks from the start of the queue. `Branch` and `Recommended` use the peek and the head sweep as implemented, with two instances, out-of-order commits, a rollback, a receive fault, and a crash. `Recommended` uses `RescanEpoch` with `RescanOnReceiveFault`.

TLC confirmed every outcome below. For the three-message configurations, an explicit-state explorer that mirrors the specs gives the same outcomes and the same distinct state counts. `ReceiveAnchored_Branch` and `ReceiveAnchored_Recommended` have about 20 million distinct states each, and `ReceiveAnchored_RescanEpoch_N4_TwoInstances` has about 57 million. They need a 64-bit JVM with a heap of several GB, and each took between 30 and 70 minutes on 32 workers. The other configurations finish in seconds.

| Configuration | Expected outcome |
|---|---|
| `ReceiveBaseline` | Holds |
| `ReceiveAnchored_Reset_NoBackstop` | Fails: a receive that read its anchor before the reset advances the high-water anchor past the rolled-back row |
| `ReceiveAnchored_Rescan_NoBackstop` | Fails: a sweep receive that skipped the locked row ends the sweep after the rollback asked for the same sweep anchor |
| `ReceiveAnchored_RescanGuarded_NoBackstop` | Fails: a rollback lowers the sweep back to the value a stale receive read, and the stale receive ends the sweep |
| `ReceiveAnchored_RescanEpoch_NoBackstop` | Holds, with rollbacks and receive faults |
| `ReceiveAnchored_RescanEpoch_N4` | Holds, with one instance, three receive slots, rollbacks, and a receive fault |
| `ReceiveAnchored_RescanEpoch_N4_TwoInstances` | Holds, with two instances, rollbacks, a receive fault, and a crash with restart |
| `ReceiveAnchored_RescanEpoch_N5` | `NoStrandedRow` holds, with one instance and three receive slots; `AllDelivered` is not checked |
| `ReceiveAnchored_OutOfOrder_NoBackstop` | Fails: the anchor passes a row whose insert commits later |
| `ReceiveAnchored_ScaleIn_NoBackstop` | Fails: another instance's anchor passes a row that a stopped instance releases |
| `ReceiveAnchored_Branch` | Holds, through the head sweep and the idle peek |
| `ReceiveAnchored_Recommended` | Holds |
| `ReceiveSingleAnchor_NoGuard_NoBackstop` | Fails: a rollback lowers the anchor to the value the peek read, and the peek result then moves the anchor past the rolled-back row |
| `ReceiveSingleAnchor_NoBackstop` | Holds, with rollbacks and a receive fault |
| `ReceiveSingleAnchor_NoBackstop_N4` | Holds, with one instance, three receive slots, rollbacks, and a receive fault |
| `ReceiveSingleAnchor_OutOfOrder_NoBackstop` | Fails: the anchor passes a row whose insert commits later |
| `ReceiveSingleAnchor_ScaleIn_NoBackstop` | Fails: another instance's anchor passes a row that a stopped instance releases |
