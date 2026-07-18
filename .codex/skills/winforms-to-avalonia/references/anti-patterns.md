# Anti-patterns

## Porting traps

1. Cloning event handlers into Avalonia code-behind
2. Using control indexes, tags, or display text as application state
3. UI prompts inside application rules or boundary adapters
4. Global static logging, notification, or state hubs used from Core
5. Pixel-cloning Designer layout instead of responsive panels
6. Hidden controls only to host commands
7. Fire-and-forget async without observed exceptions
8. Headless UI attributes mixed into non-UI unit assemblies
9. Silent behavior change without tests or decision notes
10. Deleting the legacy tree before parity evidence and decisions

## Modernization smells

- Manual Refresh as the primary UI synchronization mechanism
- Main window constructs the entire service graph
- Imperative secondary window trees in C#
- Per-click construction of stateful or expensive adapters
- Disabled compiled bindings or untyped data contexts
- Missing accessible names, focus order, or narrow-window behavior

## Long-term smells

11. Parallel host wiring that does not share application services
12. Multiple persisted-state stores without schema/version policy
13. Overwriting newer or unreadable persisted state with defaults
14. Localizing diagnostics at emit time instead of presentation time
15. God ViewModel with flag soup for modes
16. Every collaborator depending on the full main ViewModel type
17. Routing UI actions by localized display labels
18. Half-extracted modules that nothing uses
19. Unit tests that read implementation files as text
20. Unbounded allocation or recursion on untrusted input
21. Dynamic execution with unrestricted host capabilities
22. Stale async results clobbering newer state

## Agent execution smells

23. Source-text or file-existence tests disguised as coverage
24. Partial behavior wired to one surface only
25. Speculative fallbacks instead of fixing the real path
26. Dual-path compatibility after a clean-cut decision
27. Hedged designs that leave two implementations half-done
28. One host accidentally starting another host lifetime
29. Window hotkeys eating editing/navigation keys
30. Numeric empty values causing wild jumps or invalid state
31. Infinite-width tool windows and uneven responsive columns
32. Magic diagnostic strings without a catalog
33. Asking the user for screenshots instead of self-verifying layout
34. Checking tasks without command evidence
35. Tests that spawn the real desktop process unintentionally

## Allowed intentional departures

Document these as product decisions:

- Hidden gestures become explicit settings/menu items
- An old deployment mechanism is replaced by a documented release path
- High-risk environment integrations are gated or retired
- A legacy compatibility behavior is deliberately changed with acceptance evidence
