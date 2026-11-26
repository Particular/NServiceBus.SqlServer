"""Summarizes the queue layout measurements as Markdown and warns about each failed criterion.

usage: python report.py plans|reclaim|insert|soak <output dir>
"""
import json
import os
import statistics
import sys
from pathlib import Path

report = []


def say(text=""):
    report.append(text)


def table(header, rows):
    say()
    say("| " + " | ".join(header) + " |")
    say("|" + " --- |" * len(header))
    for row in rows:
        say("| " + " | ".join(str(cell) for cell in row) + " |")
    say()


def warn(message):
    print(f"::warning title=Queue layout validation::{message}")


def verdict(passed, criterion):
    if not passed:
        warn(f"Failed: {criterion}")
    return "Pass" if passed else "**Fail**"


def unique(values):
    return list(dict.fromkeys(values))


def span(values, digits=0):
    low, high = f"{min(values):,.{digits}f}", f"{max(values):,.{digits}f}"
    return low if low == high else f"{low} to {high}"


def read_json_lines(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]


def plans(directory):
    layouts = ["heap", "bucketed", "persisted"]
    statements = ["receive", "peek", "purge expired", "send", "mover", "mover with lock"]
    rows = json.loads((directory / "states.json").read_text(encoding="utf-8-sig"))
    found = {(r["state"], r["layout"], r["statement"]): r for r in rows}
    states = unique(r["state"] for r in rows)
    rcsi = rows[0]["rcsi"]

    say(f"## Plans and page reads, RCSI {rcsi}")
    say()
    say("Logical reads on the queue table for one execution:")
    table(["Table state", "Statement"] + layouts,
          [[state, name] + [found.get((state, layout, name), {}).get("reads", {}).get("q", "-") for layout in layouts]
           for state in states for name in statements])

    criteria = []
    for state in states:
        heap = found.get((state, "heap", "receive"))
        bucketed = found.get((state, "bucketed", "receive"))
        if not heap or not bucketed or "operators" not in bucketed:
            warn(f"Not measured: receive, RCSI {rcsi}, {state}")
            criteria.append([state, "Not measured", "Not measured"])
            continue

        reads = (bucketed["reads"].get("q"), heap["reads"].get("q"))
        ordered = any(o.startswith(("Index Scan Index_RowVersion", "Index Seek Index_RowVersion")) and o.endswith(" ordered")
                      for o in bucketed["operators"])
        scan = any(o.startswith("Clustered Index Scan") for o in bucketed["operators"])
        criteria.append([
            state,
            verdict(None not in reads and reads[0] <= reads[1],
                    f"RCSI {rcsi}, {state}: a bucketed receive reads {reads[0]} pages, a heap receive {reads[1]}"),
            verdict(ordered and not scan, f"RCSI {rcsi}, {state}: the bucketed receive plan")])

    say("Criteria for the bucketed receive:")
    table(["Table state", "Reads no higher than the heap", "Ordered `Index_RowVersion` read, no clustered scan"], criteria)

    say("Receive plan operators:")
    table(["Table state", "Layout", "Operators"],
          [[state, layout, "; ".join(found.get((state, layout, "receive"), {}).get("operators", ["no plan captured"]))]
           for state in states for layout in layouts])

    loops_file = directory / "loops.json"
    if not loops_file.exists():
        warn(f"Not measured: committed statements, RCSI {rcsi}")
        return

    loops = json.loads(loops_file.read_text(encoding="utf-8-sig"))
    say("Committed statements, range of the repetitions. Compare `persisted` with `bucketed` to decide on `PERSISTED`.")
    cells = []
    for name in unique(r["statement"] for r in loops):
        for layout in layouts:
            runs = [r for r in loops if r["statement"] == name and r["layout"] == layout]
            if runs:
                cells.append([name, layout, span([r["readsPerExecution"] for r in runs], 1),
                              span([r["cpuMicrosecondsPerExecution"] for r in runs])])
    table(["Statement", "Layout", "Logical reads per execution", "CPU microseconds per execution"], cells)


def reclaim(directory):
    samples = read_json_lines(directory / "reclaim.jsonl")

    def total(sample):
        return sum(sample["leafPages"].values())

    def data(sample):
        pages = sample["leafPages"]
        return pages.get("heap", pages.get("Index_Bucket_RowVersion", 0))

    def select(scenario, layout, point):
        return [s for s in samples if (s["scenario"], s["layout"], s["point"]) == (scenario, layout, point)]

    say("## Page reclaim")
    say()
    say("Leaf pages of each repetition. Data pages are those of the heap or the clustered index.")
    keys = unique((s["scenario"], s["point"]) for s in samples)
    table(["Scenario", "Point", "Heap data pages", "Heap all pages", "Bucketed data pages", "Bucketed all pages"],
          [[scenario, point] + [", ".join(str(count(s)) for s in select(scenario, layout, point))
                                for layout in ("heap", "bucketed") for count in (data, total)]
           for scenario, point in keys])

    def check(scenario, layout, point, passes, criterion):
        runs = select(scenario, layout, point)
        if not runs:
            warn(f"Not measured: {criterion}")
            return [scenario, criterion, "Not measured"]
        return [scenario, criterion, verdict(all(passes(s) for s in runs), f"{scenario}: {criterion}")]

    def released(sample):
        return sample["rows"] == 0 and total(sample) < 20

    def kept(sample):
        return sample["rows"] == 0 and data(sample) > 1000

    criteria = []
    for scenario in unique(s["scenario"] for s in samples):
        if "long transaction" in scenario:
            criteria += [
                check(scenario, "heap", "drained, 60 s", kept, "the heap keeps its pages while the transaction is open"),
                check(scenario, "bucketed", "drained, 60 s", kept, "the bucketed table keeps its pages while the transaction is open"),
                check(scenario, "bucketed", "commit, 60 s", released, "the bucketed table holds fewer than 20 leaf pages after the commit"),
                check(scenario, "heap", "commit, 60 s", kept, "the heap keeps its pages after the commit")]
        else:
            criteria += [
                check(scenario, "bucketed", "drained, 60 s", released, "the bucketed table holds fewer than 20 leaf pages at zero rows"),
                check(scenario, "heap", "drained, 60 s", kept, "the heap holds thousands of data pages at zero rows")]
    table(["Scenario", "Criterion", "Result"], criteria)


def insert(directory):
    runs = read_json_lines(directory / "insert.jsonl")
    rcsi = runs[0]["rcsi"]
    say(f"## Insert throughput, RCSI {rcsi}")
    say()
    say(f"{span([r['seconds'] for r in runs])} seconds per run. The trend is the rate in the last third of a run divided by the rate in the first third. "
        "Throughput passes when the bucketed median is at least the lowest heap run. "
        "Trend passes when the bucketed median falls no further than the heap falls in its worst run.")
    rows = []
    for concurrency in unique(r["concurrency"] for r in runs):
        by = {layout: [r for r in runs if r["concurrency"] == concurrency and r["layout"] == layout] for layout in ("heap", "bucketed")}
        if not all(by.values()):
            warn(f"Not measured: RCSI {rcsi}, {concurrency} sessions")
            continue
        rate = {k: [r["insertedPerSecond"] for r in v] for k, v in by.items()}
        trend = {k: [r["lastThirdOverFirstThird"] for r in v] for k, v in by.items()}
        spread = max(r["bucketSpread"] for r in by["bucketed"])
        errors = sum(len(r["errors"]) for v in by.values() for r in v)
        where = f"RCSI {rcsi}, {concurrency} sessions"
        rows.append([
            concurrency,
            len(rate["bucketed"]),
            *(f"{statistics.median(rate[k]):,.0f} ({span(rate[k])})" for k in ("heap", "bucketed")),
            f"{statistics.median(rate['bucketed']) / statistics.median(rate['heap']):.3f}",
            *(f"{statistics.median(trend[k]):.2f} ({span(trend[k], 2)})" for k in ("heap", "bucketed")),
            verdict(statistics.median(rate["bucketed"]) >= min(rate["heap"]), f"{where}: bucketed insert throughput"),
            verdict(statistics.median(trend["bucketed"]) >= min(1, *trend["heap"]), f"{where}: bucketed insert trend"),
            f"{spread} {verdict(spread <= 1, f'{where}: bucket sizes differ by {spread}')}",
            f"{errors} {verdict(errors == 0, f'{where}: {errors} session errors')}"])
    table(["Sessions", "Runs", "Heap inserts/s", "Bucketed inserts/s", "Ratio", "Heap trend", "Bucketed trend",
           "Throughput", "Trend", "Bucket spread", "Errors"], rows)


def soak(directory):
    lines = read_json_lines(directory / "soak.jsonl")
    rcsi = lines[0]["rcsi"]
    layouts = unique(x["layout"] for x in lines)
    minutes = {k: [x for x in lines if x["layout"] == k and "minute" in x] for k in layouts}
    summaries = {x["layout"]: x for x in lines if x.get("summary")}
    results = {}
    for layout, samples in minutes.items():
        steady = [x["receivedPerMinute"] for x in samples[1:] or samples]
        total, previous = 0.0, 0.0
        for x in samples:
            total += x["receivedPerMinute"] * (x["elapsedSeconds"] - previous) / 60
            previous = x["elapsedSeconds"]
        early = [x["receivedPerMinute"] for x in samples[1:6] or samples]
        late = [x["receivedPerMinute"] for x in samples[-5:]]
        end = samples[-1]
        data_pages = end["pages"].get("heap", end["pages"].get("Index_Bucket_RowVersion", 0))
        summary = summaries.get(layout)
        results[layout] = {
            "minutes": len(samples),
            "total": total,
            "low": min(steady),
            "high": max(steady),
            "below": min(steady) / statistics.mean(steady) - 1,
            "above": max(steady) / statistics.mean(steady) - 1,
            "trend": statistics.mean(late) / statistics.mean(early),
            "rows": end["rows"],
            "density": end["rows"] / max(1, data_pages),
            "ghosts": max(max(x["ghosts"].values()) for x in samples),
            "sampled": sorted({h for x in samples for h in x["receivePlanHashes"]}),
            "stored": summary["queryStoreReceivePlanHashes"] if summary else None,
            "plans": summary["queryStorePlans"] if summary else [],
            "errors": summary["errorMessages"] if summary else None}

    say(f"## Soak, RCSI {rcsi}")
    say()
    say(f"8 senders and 8 receivers. Layouts ran in this order on one runner: {', '.join(layouts)}.")
    measures = [
        ("Minutes sampled", lambda r: r["minutes"]),
        ("Rows received", lambda r: f"{r['total']:,.0f}"),
        ("Received per minute from minute 2", lambda r: f"{r['low']:,.0f} to {r['high']:,.0f}"),
        ("Distance from the mean", lambda r: f"{r['below']:+.1%} to {r['above']:+.1%}"),
        ("Last 5 minutes divided by minutes 2 to 6", lambda r: f"{r['trend']:.2f}"),
        ("Rows left in the queue", lambda r: f"{r['rows']:,}"),
        ("Rows per data page at the end", lambda r: f"{r['density']:.1f}"),
        ("Most ghost rows in one index or heap", lambda r: f"{r['ghosts']:,}"),
        ("Receive plan hashes in samples", lambda r: ", ".join(r["sampled"]) or "none"),
        ("Receive plan hashes in Query Store", lambda r: "not recorded" if r["stored"] is None else ", ".join(r["stored"]) or "none"),
        ("Session errors", lambda r: "not recorded" if r["errors"] is None else len(r["errors"]))]
    table(["Measure"] + layouts, [[label] + [value(results[k]) for k in layouts] for label, value in measures])

    say("Mean rows received per minute in each 10-minute block:")
    longest = max(len(v) for v in minutes.values())
    table(["Minutes"] + layouts,
          [[f"{start + 1} to {min(start + 10, longest)}"] +
           [f"{statistics.mean(x['receivedPerMinute'] for x in minutes[k][start:start + 10]):,.0f}" if minutes[k][start:start + 10] else "-"
            for k in layouts]
           for start in range(0, longest, 10)])

    for layout in layouts:
        for plan in results[layout]["plans"]:
            say(f"- {layout}: `{plan}`")
    say()

    bucketed, heap = results.get("bucketed"), results.get("heap")
    if not bucketed or not heap:
        warn(f"Not measured: soak, RCSI {rcsi}, both layouts")
        return

    where = f"Soak, RCSI {rcsi}"
    criteria = [
        ["Received per minute stays within 10% of the mean from minute 2 (assumed baseline)",
         verdict(bucketed["below"] >= -0.1 and bucketed["above"] <= 0.1, f"{where}: bucketed receive rate within 10%")],
        ["Page and ghost counts stay bounded", "Not judged. Compare the rows per data page and ghost rows; the backlog changes the page count."]]
    if bucketed["stored"] is None:
        warn(f"Not measured: {where}, plan hashes and session errors")
        criteria.append(["Receive plan hash stays the same, and no session reports an error", "Not measured"])
    else:
        criteria.append(["Receive plan hash stays the same",
                         verdict(len(bucketed["stored"]) == 1, f"{where}: bucketed receive used {len(bucketed['stored'])} plan hashes")])
        criteria.append(["No session reports an error",
                         verdict(not bucketed["errors"], f"{where}: {len(bucketed['errors'])} bucketed session errors")])
    ratio = bucketed["total"] / heap["total"]
    criteria.append([f"Receives at least as many rows as the heap (ratio {ratio:.3f}, one run each)",
                     verdict(ratio >= 1, f"{where}: bucketed received {ratio:.3f} of the heap's rows")])
    table(["Criterion for the bucketed layout", "Result"], criteria)


if __name__ == "__main__":
    try:
        {"plans": plans, "reclaim": reclaim, "insert": insert, "soak": soak}[sys.argv[1]](Path(sys.argv[2]))
    except (FileNotFoundError, IndexError) as e:
        say(f"No results to report: {e}")
        warn(f"Not measured: {sys.argv[1]}")
    text = "\n".join(report) + "\n"
    summary_file = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_file:
        with open(summary_file, "a", encoding="utf-8") as summary:
            summary.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    print(text)
