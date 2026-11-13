# Coding Standards

Analyzer rules from `Opinionated.DotNet.CodingStandards` are enforced by the build (`TreatWarningsAsErrors`). This file covers what tooling can't check.

## Comments are a last resort

Code should explain itself. Before writing a comment, try these in order:

1. **Rename.** A name that says what a variable, method or type does or holds removes the need to describe it.
2. **Extract an explainer method.** Pull a condition or block into a method whose name states its intent.

   ```csharp
   // Avoid
   // Skip items that are discontinued and out of stock
   if (item.Discontinued && item.Stock == 0) continue;

   // Prefer
   if (item.IsUnavailable) continue;
   ```

3. **Introduce an explaining variable or type.** Name an intermediate result, or give a domain concept its own type instead of a primitive.
4. **Write a test.** Expected behaviour belongs in a test name and assertions, where it can't drift out of date.

Write a comment only when none of these can carry the meaning. That is usually a **why** the code can't express: a workaround for an external bug, a non-obvious constraint, or a deliberate choice that looks wrong. Keep it short and link to the issue, ADR or doc that holds the detail.

### Don't write

- Comments that restate what the code does.
- XML doc comments (`///`) on internal or self-explanatory members. `CS1591` is disabled on purpose. Add XML docs only to public package APIs where consumers need them in IntelliSense, and only when they say more than the signature does.
- Placeholder or history notes ("added for phase 1", "changed by X"). Git and the issue tracker hold that.
- Commented-out code. Delete it; it's in git history.
- `TODO` comments without a linked issue.
