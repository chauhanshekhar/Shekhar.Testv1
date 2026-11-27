#!/bin/bash
cd /c/Testing/Shekhar.Testv1
R=docs/research/2026-11-25-ablation-2026-11-01-v3
name="${1%%|*}"; set="${1#*|}"
if [ "$name" = "parity" ]; then
  dotnet run --no-build --project src/Trading.Backtest -- --signal-parity --regime-replay --from 2026-11-01 --out "$R/parity" > "$R/parity.log" 2>&1
else
  args=(--backtest-trades --regime-replay --from 2026-11-01 --cost-per-order 20 --slippage-points 1 --dump-trades --out "$R/$name")
  [ -n "$set" ] && args+=(--set "$set")
  dotnet run --no-build --project src/Trading.Backtest -- "${args[@]}" > "$R/$name.log" 2>&1
fi
echo "$name exit=$?" >> $R/progress.txt
