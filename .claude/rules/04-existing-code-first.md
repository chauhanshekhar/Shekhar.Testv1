# Existing Code First and Validation

- Before changing anything, read the related files, find the root cause, and recommend the
  minimal fix. Preserve naming, logging, and DI patterns. No unrelated refactoring.
- Reuse existing abstractions (`IPreSignalGate`/`IPostSignalGate`, `StrategySettingsService`,
  `IstClock`, `TradingConst`) instead of inventing parallel ones. If a command or doc names a type
  that does not exist in the code, say so and use the real one — do not create the imagined type.
- `Trading.Tests` exists (xUnit). New behaviour gets a focused test next to the existing ones;
  never weaken a test to make it pass. (`AGENTS.md`'s "No automated tests exist" is out of date.)
- Validation before calling a change done: `dotnet build TradingPlatform.slnx` and
  `dotnet test src/Trading.Tests` pass. Report pre-existing failures separately from new ones.
  Deployment checks (Docker build, API/workers start, SQL/Groww/SignalR connect) are listed for
  the user — never deploy.
- Every change report includes: Problem, Root Cause, Evidence, Affected Files, Minimal Fix, Risk,
  Validation, Deployment Impact (see `AGENTS.md` Response Format).
