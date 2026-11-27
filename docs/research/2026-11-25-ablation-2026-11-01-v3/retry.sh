#!/bin/bash
cd /c/Testing/Shekhar.Testv1
R=docs/research/2026-11-25-ablation-2026-11-01-v3
for line in "off-EnableTrendFilter|EnableTrendFilter=0" "off-EnableAtrFilter|EnableAtrFilter=0" "off-EnableIndexStopFloor|EnableIndexStopFloor=0" "baseline-end|"; do
  name="${line%%|*}"
  for try in 1 2 3; do
    $R/run_one2.sh "$line"
    [ -f "$R/$name/backtest-trades.csv" ] && break
  done
  echo "$name done" >> $R/retry-progress.txt
done
