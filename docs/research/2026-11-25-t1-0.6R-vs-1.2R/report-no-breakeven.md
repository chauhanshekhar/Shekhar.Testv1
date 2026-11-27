# Removing the breakeven arm (2026-11-25)

Same method as the T1 reports: `--backtest-trades --from 2026-10-14`, live settings, costs included, `--set` overrides
(research only; nothing in the database or live code changed). Outputs: `docs/research/2026-11-25-no-breakeven*`,
`docs/research/2026-11-25-no-steplock-only`.

Finding: `EnableBreakevenArm=0` alone is a no-op while `EnableStepLock=1`. `StepLockLevel` returns the entry price once the
peak reaches entry + BreakevenBufferPoints - the same stop the breakeven arm sets. The partial sale at the arm is already off
(`EnableBreakevenPartialSale=0`). Removing breakeven therefore means turning off BOTH the arm and the step-lock.

| Variant | Trades | Win % | Net Rs | PF | Max DD | PROTECTED_SL_HIT |
|---|---:|---:|---:|---:|---:|---:|
| Current (arm + step-lock) | 89 | 47% | +52,610 | 1.76 | 14,588 | 28 |
| Arm off only | 89 | 47% | +52,610 | 1.76 | 14,588 | 28 (identical, every trade) |
| Step-lock off only | 89 | 47% | +51,705 | 1.75 | 14,588 | 27 |
| Arm + step-lock off | 80 | 56% | +59,446 | 1.77 | 14,996 | 9 |

Arm + step-lock off, versus current: CALL +53,323 -> +45,713 (PF 2.38 -> 1.87, max DD 8,522 -> 15,624); PUT -712 -> +13,733
(PF 0.98 -> 1.55); first half +9,690 -> +11,006, second half +42,920 -> +48,440; better on 6 days, worse on 7, equal on 14.
Entries differ (79 matched, 10 only in current, 1 only in variant) because the one-position rule shifts them.

Caveats: the replay samples one option price per minute and so under-counts intra-minute protected-stop hits (live signal
10449 on 2026-11-25 was stopped that way and the replay was not), which flatters the no-protection variant. 27 sessions.
Removing the arm removes the protected stop below T1 - CLAUDE.md lists protected stop loss as never to be weakened without
explicit approval.
