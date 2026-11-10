# AltTabFlow — developer/AI coding agent instructions

Before editing code in any new session, read and respect these repository-owned sources in order:

1. **docs/PROJECT_HANDOFF.md** — definitive quick start / current state.
2. **docs/DEVELOPMENT_HISTORY.md** — complete 1.1.1→current history, previous regressions, PR/CI references.
3. **docs/ARCHITECTURE_AND_INVARIANTS.md** — input routing, MRU, foreground window, settings, UI behaviors that must not silently regress.
4. **docs/TEST_MATRIX.md** — exact unit/UI tests vs native/physical keyboard regression coverage.
5. **docs/specs/** and most recent 1.1.x change specification, then inspect actual current `main` source, Issues #1 and recent Actions.

## Delivery contract

- Preserve the current source and user-defined behavior. Newer explicit user requirements supersede obsolete specs; record the change.
- Implement changes on a branch, submit a PR, and include tests, changelog, release checklist and source specification. Never bypass failing CI.
- Confirm .NET 8 WinForms Windows compilation and basic/UI regression before merging. Distinguish Windows Server CI from a real interactive Windows 10/11 PC, administrator game privileges, P4, physical input, mixed DPI and Z-order.
- Do **not** describe the 1.1.7 handoff's 257 passed tests as having all been independently reproduced in CI: the successful [PR #5 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42777258155) covers 161 basic + 71 UI, total 232. Remaining 25 unique tests and physical game/window interactions require separate Windows testing.
- Never hide/convert failed focus tests to passing: the documented normal-permission versus elevated MapleStory foreground conflict must remain recorded.
- Maintain `.gitattributes` LF source policy and validate `SOURCE-SHA256.txt` on updates.
- No unverified production `v1.1.x` release or tag. Candidate EXEs and screenshots are not proof of all native tests.
- Do not commit `settings.json`, user window titles, process paths, private diagnostic logs, account secrets or personal machine configuration.

## New-session starting prompt

“Review this repository's `AGENTS.md`, `docs/PROJECT_HANDOFF.md`, `docs/DEVELOPMENT_HISTORY.md`, `docs/ARCHITECTURE_AND_INVARIANTS.md`, `docs/TEST_MATRIX.md`, latest code/PR/Actions and Issue #1. Report the verified current version and unresolved validations first, then continue the requested work.”

The private GitHub repository is the durable project record, not any particular ChatGPT chat session or local temporary file.
