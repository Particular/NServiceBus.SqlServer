# Receive from an anchor and recover stranded rows with sweeps

Implemented in [#1847](https://github.com/Particular/NServiceBus.SqlServer/pull/1847), which links each pull request in the stack.

## Context

Competing receivers read the queue table in sequence number order and skip locked rows. Without an anchor, every receive starts at the head of the queue index and passes rows that other receivers have deleted or still hold, so database load grows faster than the number of receivers ([#1784](https://github.com/Particular/NServiceBus.SqlServer/issues/1784)). On Azure SQL with 8 instances, each receive used 32 to 41 logical reads, and each message cost 3.7 to 5.7 ms of database CPU.

Sequence numbers do not follow commit order. A sender can commit a lower sequence number after a receiver has passed it, and a receive can roll back after the receiver has moved past its row. A receiver that seeks past the head must find those rows again. Receives run concurrently with each other and with the peek, so the position a receive starts from can change while its query runs.

## Decision

- Each receiver keeps a `ReceiveCursor`. Receives seek after the highest received row version, which receives and peeks move forward. While the queue stays busy, the peek seeks from the same position.
- A sweep finds rows behind that position. While a sweep runs, receives seek after the sweep anchor instead. A local rollback or a peek that finds a lower row starts or lowers a sweep. The receive loop starts a sweep from the head when it gets a processing slot after `HeadSweepInterval` has elapsed. After an empty peek, the next peek reads the whole queue.
- Every sweep request increments a sweep generation. A sweep receive moves or ends the sweep only if the generation it read is still current. One lock protects the highest received row version, the sweep anchor, and the generation ([#1860](https://github.com/Particular/NServiceBus.SqlServer/pull/1860)).
- Anchoring is on by default. The `Receiving.DisableAnchoring` AppContext switch turns it off for this major version ([#1857](https://github.com/Particular/NServiceBus.SqlServer/pull/1857)).

## Consequences

- In the Azure benchmark in #1847, anchoring raised Azure SQL throughput 3.0 to 4.7 times and cut database CPU per message by 88 to 94%. On Azure PostgreSQL with 8 instances at concurrency 8, it raised throughput 5 to 34%.
- A row that commits out of order behind the anchor waits for a peek to find it or for the next head sweep. A head sweep starts the first time the receive loop gets a processing slot after `HeadSweepInterval`, 5 seconds by default, has elapsed, so the interval does not bound the wait. The [receive models](../../models/receive/README.md) strand rows under every cursor policy when commits arrive out of order or an instance stops, so the head sweep must stay even though it adds load. The delay is accepted because a busy receive loop gets slots often and the interval is configurable. Values below one second are raised to one second.
- A sweep receive must read the sweep anchor and generation together, and must check the generation and update the sweep in one step. One lock protects the highest received row version, the sweep anchor, and the generation. It covers only field reads and writes, never a database call. If the sweep receive compares anchor values instead of a generation, a rolled-back row waits for the next head sweep ([#1848](https://github.com/Particular/NServiceBus.SqlServer/pull/1848), #1860).
- On an 8 vCore Azure PostgreSQL server, 8 instances at concurrency 32 delivered less than 8 instances at concurrency 8, with and without anchoring. With anchoring, throughput was 2,235 to 2,904 msg/s at concurrency 32 and 4,293 to 9,371 msg/s at concurrency 8. This change accepts that result and leaves batch limits open. In separate runs of a 10 ms drain with the #1857 cursor, the free-slot limit raised 8×32 throughput from 2,309 to 4,520 msg/s, about half of the 8,212 msg/s that 8×16 reached without a limit. Lowering concurrency to 16 helped more than any batch limit at 32.
- The AppContext switch is removed in the next major version. [#1856](https://github.com/Particular/NServiceBus.SqlServer/issues/1856) collects reasons to keep it.

## Alternative approaches

The figures below come from the Azure setup described in #1847, as medians of three rounds. The table is a drain of 25,000 messages with a 10 ms handler on Azure PostgreSQL. The anchored cursor in these runs is the one from #1857, before #1860. With and without the free-slot limit, #1860 was within 5% of it on Azure PostgreSQL.

### Single anchor raised only by the peek

This cursor keeps one anchor that only the peek raises and a rollback lowers. `models/receive/ReceiveSingleAnchor.tla` models it. It holds less state than the anchored cursor and needs no sweep generation. The measured build is a port of the model that the model's authors have not reviewed. The port runs the head peek on the `HeadSweepInterval` timer instead of every n-th peek, and a batch takes one receive when no slot is idle.

In the 10 ms drain, the single anchor kept up with the #1857 cursor only under the free-slot limit, and that limit cut throughput at 8×8 and 8×16. It is rejected for that reason.

| Cursor and batch policy, msg/s | 8×8 | 8×16 | 8×32 |
|---|---:|---:|---:|
| #1857 cursor, no batch limit | 4,460 | 8,212 | 2,309 |
| #1857 cursor, free-slot limit | 3,897 | 6,388 | 4,520 |
| Single anchor, free-slot limit | 3,909 | 6,275 | 4,593 |
| #1857 cursor, max-concurrency limit | 4,387 | 7,573 | 4,035 |
| Single anchor, max-concurrency limit | 4,224 | 5,732 | 3,677 |

Without a limit, one batch can cover the whole backlog, so receives keep seeking from near the head. A local run at 8×32 measured the same throughput as anchoring off (2,514 against 2,537 msg/s); this case was not measured on Azure. With the max-concurrency limit at 8×16, the single anchor read 1.8 times as many buffer blocks per receive as the #1857 cursor (69.8 against 39.0). In the no-op drain, the two cursors were within 4% of each other under either limit, and both limits were slower than no limit at 8×8 and faster at 8×16 and 8×32. If batching moves to free-slot limits for another reason, the single anchor becomes competitive and should be reconsidered.

### Lock-free swap of an immutable sweep state

[#1858](https://github.com/Particular/NServiceBus.SqlServer/pull/1858) uses the same generation check. It stores the sweep anchor and generation in one immutable object and swaps it with a compare-exchange. The models show that it delivers every message, and its throughput was indistinguishable from the lock (#1860). It is rejected because it allocates on every sweep change and every failed swap, and the lock is simpler to reason about.

### Compare sweep anchor values

#1848 stops a sweep receive from moving the sweep past an anchor that a rollback lowered while the receive ran. The comparison cannot detect a rescan that leaves the anchor at the value the receive read, or a sweep that moves on and is lowered back to the same value. With the head sweep disabled, the models strand a rolled-back row under this guard. #1860 replaces it with the generation check.

### Batch limits

A free-slot limit starts only as many receives per peek as there are idle processing slots. On Azure PostgreSQL, it raised 10 ms drain throughput at 8×32 from 2,309 to 4,520 msg/s. It cost 12.6% at 8×8 and 22% at 8×16 in the 10 ms drain, and 28% in a no-op drain of 50,000 messages at 8×8 (6,626 against 9,251 msg/s). On Azure SQL, no batch limit was faster than no limit in any workload. Where the rounds did not overlap, the limits were slower, by up to 22% in the no-op drain at 8×32 (2,327 against 2,969 msg/s). A batch still starts a receive for every message the peek found. A protective limit for PostgreSQL at high concurrency does not depend on the cursor and can be a separate change.

### Public setting to choose the mode

`QueuePeekerOptions.EnableReceiveAnchoring` and `EnablePeekAnchoring` were removed before they shipped, and no public setting replaces them (#1857). A public setting would tell users that each mode suits some endpoints, and the documentation would have to explain the trade-off so they can choose. The benchmarks found no workload where that trade-off exists. In every workload measured on Azure and locally, anchoring delivered the same or higher throughput at the same or lower database cost (#1847). Anchoring is therefore the only supported mode.

The `DisableAnchoring` AppContext switch is not part of the public API. It exists for a case the investigation did not cover, logs a warning at startup, and is removed in the next major version. #1856 collects reports from anyone who needs it. One switch covers receive and peek anchoring, because peek anchoring has no effect without receive anchoring.
