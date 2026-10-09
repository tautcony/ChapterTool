## 1. Regression baseline

- [x] 1.1 Add failing browser assertions for independent history and expression dialogs.

## 2. Dialog implementation

- [x] 2.1 Add the shared native dialog lifecycle and one active-dialog state. Migrate existing dialogs and block background actions.
- [x] 2.2 Move history into a dialog and preserve branch navigation.
- [x] 2.3 Extract expression draft and debounce ownership. Preserve cancellation, validation, and one-step apply.
- [x] 2.4 Add staged advanced export options and template loading. Preserve persistence on Apply.
- [x] 2.5 Correct responsive main and dialog sizing. Add shared translations and regenerate locale JSON.

## 3. Browser gates

- [x] 3.1 Update affected workflows and add geometry, focus, hit-target, cancellation, and background-isolation checks.
- [x] 3.2 Add dialog visual states, separate visual outputs, and published-site modal smoke coverage.
- [x] 3.3 Add PR WebKit and fixed-Linux visual gates.

## 4. Verification and documentation

- [x] 4.1 Run affected WASM unit tests and the full browser suite across three engines. Fix regressions.
- [x] 4.2 Generate and inspect fixed-Linux snapshots. Run visual checks and capture default, wide, narrow, short, and landscape evidence.
- [x] 4.3 Update browser ownership and usage documentation. Record validation and physical-device limitations. Validate the OpenSpec change.
