## Why

ChapterTool can parse MPLS, CLPI, and INDEX files. The current native BDMV importer still cannot discover titles correctly.

An HDMV entry in `index.bdmv` references a MovieObject. It does not reference an MPLS file. A BD-J entry references a BDJO file. The current importer converts an HDMV object identifier directly to an MPLS name. This conversion is invalid.

The importer also delegates BDMV titles to standalone MPLS import. This operation creates one entry for each PlayItem. A BDMV title must create one entry for the complete playlist.

This change will add native navigation parsing and bounded title discovery. eac3to reference manifests will define the required output.

## What Changes

- Add a Core MovieObject parser.
- Add a bounded Core HDMV navigation resolver.
- Add a Core BDJO parser for accessible-playlist declarations.
- Add typed INDEX references for HDMV MovieObject identifiers and BD-J Object names.
- Add an Infrastructure playlist scanner with structural duplicate and repeat filtering.
- Add a deterministic discovery policy that merges navigation evidence and playlist-scan evidence.
- Add a Core aggregate MPLS projection for complete-playlist chapters and clip collections.
- Normalize disc-root, `BDMV` directory, and `index.bdmv` input paths.
- Route normalized BDMV inputs to the corrected native importer.
- Use eac3to only as a reference oracle and as an explicit fallback for unsupported dynamic BD-J navigation.
- Preserve standalone `.mpls` import behavior.

## Capabilities

### New Capabilities

- `bdmv-native-clpi-parsing`: Parse CLPI files in Core.
- `bdmv-native-index-parsing`: Parse INDEX files in Core.
- `bdmv-native-navigation-parsing`: Parse MovieObject and BDJO files. Resolve bounded HDMV navigation events.
- `bdmv-native-directory-import`: Discover and import complete chapter-bearing playlists without a required external tool.

### Modified Capabilities

- `disc-playlist-media-importers`: Accept all BDMV input shapes and expose aggregate clip collections.

## Scope Limit

The change will not execute BD-J JAR files or Xlets. BDJO declarations and bounded playlist scanning will provide fallback evidence. The importer must diagnose dynamic BD-J navigation that it cannot resolve.

## Impact

- Core gains MovieObject, HDMV resolver, BDJO, and aggregate MPLS components.
- Infrastructure gains path normalization, playlist scanning, and evidence merging.
- Existing invalid INDEX-to-MPLS behavior must be removed.
- Existing completed CLPI and INDEX parser work remains in scope.
- Automated parity tests will use committed eac3to manifests.
- The local libbluray source is a format and behavior reference. The implementation must be an independent C# implementation.
