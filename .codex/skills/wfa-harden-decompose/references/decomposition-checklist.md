# Decomposition checklist (generic)

## Session smells

- [ ] Multiple booleans for the same mode
- [ ] Parallel “current” and “backup” fields for one concept
- [ ] Scrape controls before every command
- [ ] UI actions switch on localized display text
- [ ] Collaborators constructed with the full main ViewModel
- [ ] A secondary surface constructs a private copy of application services
- [ ] A second host duplicates composition privately

## Security smells

- [ ] External data is consumed without size/depth/allocation bounds
- [ ] External resources are reachable without explicit policy
- [ ] Dynamic execution can access host capabilities without a budget or sandbox
- [ ] Process arguments are built as an unsafe shell string

## Headless smells

- [ ] Headless attributes in unit project
- [ ] Mixed Headless + unit same host
- [ ] Control-exists-only tests
- [ ] Long Task.Delay instead of dispatcher pump

## Extraction slice

1. Name collaborator and pure inputs/outputs
2. Move state machine; leave INPC on VM
3. Keep/add anti-stale tests
4. Wire composition; delete dead types
5. Focused unit + Headless if UI path
6. Full solution gate
7. Update ownership map

## Spec alignment mini-template

| Spec scenario | Implementation | Match? |
| --- | --- | --- |
| … | … | ✅/❌ |
