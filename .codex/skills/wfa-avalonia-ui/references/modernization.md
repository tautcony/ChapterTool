# Modernization checklist (generic)

## High

- [ ] Composition root / DI — not MainWindow constructing the world
- [ ] Observable ViewModel
- [ ] Remove pervasive manual Refresh
- [ ] Secondary windows as XAML + VM
- [ ] Discovered boundary dependencies injected

## Medium

- [ ] Compiled bindings + x:DataType
- [ ] Async commands observed
- [ ] Remove hidden command shims
- [ ] Capability filters match the application's declared support

## Headless isolation

- [ ] Headless tests in a dedicated project
- [ ] Serial collection inside Headless process
- [ ] Never merge Headless into unit assemblies for convenience
- [ ] Behavior tests over control-exists assertions
- [ ] Window hotkeys do not steal keys from focused text editors
- [ ] Tool windows have max width / scroll; match main styles
- [ ] No accidental desktop process launch from “unit” tests
- [ ] Accessibility names, focus order, and keyboard behavior verified
- [ ] Default, narrow, wide, scaling, and localized layouts checked

## Commands

```bash
dotnet test tests/<Product>.Avalonia.Tests --no-restore
dotnet test tests/<Product>.Avalonia.Headless.Tests --no-restore
dotnet build src/<Product>.Avalonia --no-restore
```

Run test projects sequentially if they share `obj/`.
