#!/usr/bin/env bash
# Runs one queue layout measurement against the server in BENCH_CS and writes its results to OUTPUT_DIR.
# usage: run.sh plans <RCSI> | reclaim | insert <RCSI> | soak <RCSI> <layout> <layout>
set -euo pipefail

scripts=$(cd "$(dirname "$0")" && pwd)
sql="$OUTPUT_DIR/sql"
mkdir -p "$OUTPUT_DIR"

run() {
    dotnet run "$scripts/$1.cs" -- "${@:2}"
}

run extract-sql "$sql"

case $1 in
    plans)
        run plans "$sql" "$OUTPUT_DIR/states.json" "$2" states
        run plans "$sql" "$OUTPUT_DIR/loops.json" "$2" loops
        ;;
    reclaim)
        run reclaim "$sql" "$OUTPUT_DIR/reclaim.jsonl" "$RECLAIM_REPETITIONS"
        ;;
    insert)
        run insert "$sql" "$OUTPUT_DIR/insert.jsonl" "$2" "$INSERT_SECONDS" "$INSERT_REPETITIONS" 1,16,32,64
        ;;
    soak)
        run soak "$sql" "$OUTPUT_DIR/soak.jsonl" "$SOAK_MINUTES" "$2" "${@:3}"
        ;;
    *)
        echo "Unknown measurement: $1" >&2
        exit 1
        ;;
esac
