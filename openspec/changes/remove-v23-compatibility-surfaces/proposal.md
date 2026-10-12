## Why

The exact frame-rate and editable-document APIs now provide the supported lossless path. The remaining numeric facades and no-op UI shims duplicate state and invite precision loss. Remove them in the prepared 23.4.0 release as explicitly requested.

## What Changes

- **BREAKING** Remove retained numeric Core and Node content APIs and the transform-bearing export options. Keep canonical rate values, tick strings, explicit operations, and serialization APIs.
- Rename `LegacyMplsCode` to `MplsFrameRateCode` while preserving its current values and selector behavior.
- Remove the old expression toggle and unused expression settings port method, and bind the completion converter directly to its implementation.
- Stop reading obsolete browser settings storage keys. Keep the current `chaptertool.wasm.settings` key.

## Capabilities

### New Capabilities

- `public-frame-rate-contracts`: Define the canonical-only public Core and Node rate/content contracts after numeric facade removal.

### Modified Capabilities

- `one-shot-transform-compatibility`: Remove transform-bearing compatibility export entry points; retain explicit transform and serialization workflows.
- `wasm-settings-shortcuts`: Read browser settings only from the current storage key.

## Impact

Core and Node public contracts, the Node TypeScript package and generated declarations, Avalonia and WASM selectors, browser settings initialization, tests, release notes, and API documentation change. Existing consumers of removed numeric APIs or old export options must migrate to canonical `FrameRateValue`/exact Node values, explicit content operations, and pure serialization.
