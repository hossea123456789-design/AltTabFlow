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
- Do **not** describe the current handoff's **302 passed tests** as all independently reproduced in CI. Version 1.1.8 targets **206 basic + 71 UI = 277 CI tests**, and the remaining 25 unique Peek/menu/focus tests plus physical P4/game input require separate Windows verification. The 1.1.8 uploaded handoff reports real user confirmation of ordinary and pinned shortcuts; this is source-reported, not a local or CI repetition.
- v1.1.8 invariants: reconcile stale *unintercepted* key records only; never infer intercepted Tab/arrow releases from async zero; 16ms hook timer only during active sessions; no duplicate Commit; do not inherit pinned state when a plain gesture starts.
- Never hide/convert failed focus tests to passing: the documented normal-permission versus elevated MapleStory foreground conflict must remain recorded.
- Maintain `.gitattributes` LF source policy and validate `SOURCE-SHA256.txt` on updates.
- v1.1.9: in-app update fetches only latest successful main GitHub Actions candidate, verifying ZIP SHA-256 and executable version; private repo PAT (Contents/Actions:read) goes to Windows Credential Manager, never settings/logs. Updater helper replaces EXE only after old process exits.
- v1.1.9: game mode UAC must be approved before the original process is told to quit; token takeover across ordinary/elevated singleton must be tested on a real Windows PC. Candidate archive must keep EXE/game scripts in a single folder.
- No unverified production `v1.1.x` release or tag. Candidate EXEs and screenshots are not proof of all native tests.
- Do not commit `settings.json`, user window titles, process paths, private diagnostic logs, account secrets or personal machine configuration.

## New-session starting prompt

“Review this repository's `AGENTS.md`, `docs/PROJECT_HANDOFF.md`, `docs/DEVELOPMENT_HISTORY.md`, `docs/ARCHITECTURE_AND_INVARIANTS.md`, `docs/TEST_MATRIX.md`, latest code/PR/Actions and Issue #1. Report the verified current version and unresolved validations first, then continue the requested work.”

The private GitHub repository is the durable project record, not any particular ChatGPT chat session or local temporary file.
