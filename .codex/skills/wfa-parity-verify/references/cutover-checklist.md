# Legacy cutover checklist (generic)

## Before freezing legacy

- [ ] New solution is default for contributors
- [ ] CI builds new solution primarily
- [ ] High/Blocker gaps filed

## Before deleting legacy

- [ ] No Blocker without fix or signed waiver
- [ ] Required capabilities and integrations implemented or explicitly retired
- [ ] Settings migration covers needed keys
- [ ] Packaging path documented
- [ ] Licenses still valid without legacy folders
- [ ] Fixtures moved under new tests if still needed
- [ ] Stakeholders accept intentional incompatibilities

## Delete sequence

1. Remove from solution/CI
2. Tag/branch last legacy tree
3. Delete directories
4. Grep for leftover path references
5. ChangeLog cutover note

## Waiver template

```markdown
### Waiver: <feature>
- Decision:
- Date / approver:
- User impact:
- Revisit if:
```
