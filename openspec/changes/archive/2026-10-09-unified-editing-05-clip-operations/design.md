## Context

`ClipSession` keeps original clip groups and combined output in parallel. The target model treats segment boundaries as structural metadata around the one current chapter document.

## Goals / Non-Goals

**Goals:** Make merge, split, and append reversible document transactions over current content.

**Non-Goals:** Provide a hidden pre-merge content backup, auto-truncate cross-segment ends, or persist history.

## Decisions

- Represent segment identity, source metadata, interval, and media placement as track structure. Do not store chapter arrays in boundaries.
- Implement merge and split as candidate builders with explicit identity maps; preserve IDs for surviving chapters and allocate new track IDs.
- Use left-closed/right-open intervals. Assign a marker at total duration to the final segment.
- Require an explicit policy for incompatible frame rates before combining.
- Treat append source reading as asynchronous candidate construction and use the session token at publication.

## Risks / Trade-offs

- [Legacy restore UI implies restoring original content] → Rename the action to “Split by boundaries” and test that post-merge edits survive.
- [End times can cross split boundaries] → Return a localized conflict with no commit; do not truncate implicitly.
