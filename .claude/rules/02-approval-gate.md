# Approval Gate

Two kinds of change need an explicit go-ahead from the user in the same conversation ("Apply
Changes", "Proceed", "Implement", "Approved", "Go ahead", or an unambiguous equivalent — "looks
good" or silence is not approval). Approval for one change does not carry over to another.

## Code changes (`src/`)

Propose first: root cause/intent, files to touch, the diff, risk, and how it will be validated.
Then stop and wait.

## Database changes (any environment)

Read-only `SELECT` queries for analysis may run freely against the database the user names.
Anything that writes — `INSERT`/`UPDATE`/`DELETE`/`MERGE`, DDL, `dotnet ef database update` — is
**never executed by Claude**. Write it as a script in `docs/` (idempotent `IF EXISTS ... UPDATE
ELSE INSERT`, with a commented ROLLBACK block and a VERIFY query — follow
`docs/set-banknifty-rule-kill-switch.sql`) and hand it to the user to run.

## Always free

Reading code, read-only queries, and writing analysis/report files under `docs/research/` or
`docs/*.sql` scripts that are not executed.
