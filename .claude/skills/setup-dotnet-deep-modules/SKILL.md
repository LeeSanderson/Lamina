---
name: setup-dotnet-deep-modules
description: Make each project in a .NET solution a deep module, with implementation hidden as `internal` in sub-namespaces and reachable only through public types in the project's root namespace, enforced by an architecture test project. User-invoked.
disable-model-invocation: true
---

# Setup .NET Deep Modules

Make every module in this solution a **deep module**: a lot of behaviour behind a small interface. A module is one class-library project. Its public surface is its **entry points**: the `public` types in the project's root namespace (the files at the project root). Everything in sub-folders/sub-namespaces is `internal`. This skill adds an architecture test project that makes the entry points the only way in, then proves the rules bite.

For the vocabulary (deep module, interface, seam, depth), call the Skill tool with "codebase-design" and use its language throughout.

## The shape this enforces

```
src/
  <Prefix>.<Name>/
    <Prefix>.<Name>.csproj
    OrderService.cs     ← an entry point (public, root namespace). Use this from outside.
    OrderQueries.cs     ← another entry point. Modules may expose SEVERAL.
    Lib/                ← implementation: `internal`, namespace <Prefix>.<Name>.Lib, free to use each other.
tests/
  <Prefix>.<Name>.Tests/   ← xUnit tests; reference the module, see only its public types.
  <Prefix>.Architecture.Tests/
```

The public surface is the module's **root-namespace public types**, not one designated facade class. By convention implementation lives in `Lib/`. The rule itself is general: *any* public type in *any* sub-namespace is a violation, so you never extend the rules to add a folder.

Four rules, all enforced as failing tests:

1. **Entry-point boundary**: no `public` type lives outside the module's root namespace. The compiler then stops other modules and app code from reaching implementation.
2. **Intra-module freedom**: a module's own types use each other freely (`internal` is assembly-wide; no rule needed).
3. **Tests through the entry points**: no module assembly declares `InternalsVisibleTo`. Test projects see exactly what callers see. Integration tests across modules are fine; reaching into internals is not.
4. **No cycles**: no namespace dependency cycles inside a module. (MSBuild already rejects project-reference cycles.)

**Entry points, not a god facade.** Because the public surface is *every* root-namespace type, a module can expose several small entry points (`OrderService`, `OrderQueries`) instead of funnelling everything through one huge `OrdersFacade`. Keep entry points small and hide implementation in `Lib/`.

Layering (which modules may reference which) is a *different* concern and is left as a commented stub in the architecture tests for this repo to fill in.

## Steps

### 1. Detect the environment

- **Solution file**: the `.sln`/`.slnx` at the repo root. Use `dotnet sln` for every add.
- **Namespace prefix**: from existing projects' `<RootNamespace>`/assembly names, else the solution name. Each module's `RootNamespace` must equal its `AssemblyName` (the default); the rules derive the root namespace from the assembly name.
- **Layout**: `src/` for modules and `tests/` for test projects, unless the repo already has a different obvious convention; confirm with the user if so.
- **Existing setup**: check for an existing architecture test project and any `InternalsVisibleTo` (in `.csproj` items or `AssemblyInfo.cs`). Don't silently delete existing `InternalsVisibleTo`; list them and ask.

**Done when:** solution, prefix, layout and existing-setup status are all known.

### 2. Create the architecture test project

```
dotnet new xunit -o tests/<Prefix>.Architecture.Tests
dotnet sln add tests/<Prefix>.Architecture.Tests
dotnet add tests/<Prefix>.Architecture.Tests package AwesomeAssertions
dotnet add tests/<Prefix>.Architecture.Tests package TngTech.ArchUnitNET.xUnit
```

Add a project reference to every module project.

**Done when:** the project builds and references every module.

### 3. Write the rules

Create `tests/<Prefix>.Architecture.Tests/ModuleBoundaryTests.cs`:

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using AwesomeAssertions;

namespace <Prefix>.Architecture.Tests;

public class ModuleBoundaryTests
{
    // One line per module: any public type from that module's assembly.
    public static readonly Assembly[] Modules =
    [
        typeof(<Prefix>.Example.ExampleService).Assembly,
    ];

    private static readonly Architecture Architecture =
        new ArchLoader().LoadAssemblies(Modules).Build();

    public static TheoryData<string> ModuleNames() =>
        new(Modules.Select(m => m.GetName().Name!));

    private static Assembly Module(string name) =>
        Modules.Single(m => m.GetName().Name == name);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Only_root_namespace_types_are_public(string module)
    {
        Module(module).GetExportedTypes()
            .Where(t => t.Namespace != module)
            .Select(t => t.FullName)
            .Should().BeEmpty("only a module's root namespace is its interface; make these internal");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Internals_are_not_visible_to_other_assemblies(string module)
    {
        Module(module).GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .Should().BeEmpty("tests and callers go through the entry points");
    }

    [Fact]
    public void Namespaces_have_no_cycles()
    {
        SliceRuleDefinition.Slices()
            .Matching("<Prefix>.(**)")
            .Should().BeFreeOfCycles()
            .Check(Architecture);
    }

    // Layering stub: which modules may reference which. Fill in per repo, e.g.
    // ArchRuleDefinition.Types().That().ResideInNamespace("<Prefix>.Domain")
    //     .Should().NotDependOnAny(ArchRuleDefinition.Types().That().ResideInNamespace("<Prefix>.Infrastructure"))
    //     .Check(Architecture);
}
```

The ArchUnitNET slice API has shifted between versions; if `Namespaces_have_no_cycles` doesn't compile, adapt it to the installed version rather than deleting it.

**Done when:** the four rules are present (cycles + boundary + no `InternalsVisibleTo`, plus the layering stub) and the project builds.

### 4. Wire it into the checks

`dotnet test` at the solution root already runs it. If the repo has a CI workflow or build script that runs a filtered subset of tests, make sure the architecture project is included. If there is no CI, tell the user to run `dotnet test` in it.

**Done when:** the architecture tests run as part of the same command as the other tests.

### 5. Scaffold the example module

Create a committed `src/<Prefix>.Example/` (class library, added to the solution and referenced from the architecture tests) as a copy-me template:

- `ExampleService.cs`: a `public` entry point in namespace `<Prefix>.Example`. One method that delegates to an internal type (so the module is visibly *deep*, not a pass-through).
- `Lib/Impl.cs`: an `internal` type in namespace `<Prefix>.Example.Lib`, used by `ExampleService`.
- `tests/<Prefix>.Example.Tests/ExampleServiceTests.cs`: xUnit + AwesomeAssertions, constructs only `ExampleService` and asserts against its public method.

Tell the user this is a starter template to copy or delete.

**Done when:** the example module exists, exposes its behaviour through a root-namespace public type, and hides `Impl` as `internal` in `Lib/`.

### 6. Prove the rules bite

This is the completion criterion for the whole skill: a rule that doesn't fail on a violation is worthless.

1. Run `dotnet test tests/<Prefix>.Architecture.Tests`. It must **pass**.
2. Temporarily make `Lib/Impl` `public`. Run again; `Only_root_namespace_types_are_public` must **fail** naming `Impl`. Revert.
3. Temporarily add `<InternalsVisibleTo Include="<Prefix>.Example.Tests" />` to the example `.csproj`. Run again; `Internals_are_not_visible_to_other_assemblies` must **fail**. Revert.
4. Run once more; it must **pass**.

**Done when:** you have observed pass, fail, fail, pass. If a violation does not fail, the rules are not wired correctly, so fix before finishing.

### 7. Document the convention

Write a `README.md` in `src/` covering: the module layout (public entry points at the project root/root namespace, `Lib/` for `internal` implementation, tests in `tests/<Module>.Tests`), "use a module only through its public root-namespace types; never add `InternalsVisibleTo`", and how to run the architecture tests. **Discourage god facades** explicitly: expose several small entry points instead. Keep it to the copy-me layout plus the four rules in one paragraph each.

Then add a **context pointer** to it from the repo's agent-instructions file (`AGENTS.md` if present, else `CLAUDE.md`, creating `AGENTS.md` if neither exists). One line is enough, e.g. `Projects are deep modules: see [src/README.md](./src/README.md) before adding or using one.`

**Done when:** `src/README.md` exists and discourages god facades, and the agent-instructions file links to it.

## Notes

- Public vs internal is decided by **namespace depth**: root-namespace types may be public; anything in a sub-namespace must be internal. Any sub-folder is private, so a new folder never needs a rule change. Adding an entry point is just adding a public root-level type.
- Modules are **flat**: one project per module, no module nested inside another. A module's internals may nest as deep as you like.
- Test internal seams through the public entry points. If that is painful, the module is probably the wrong shape; reach for `codebase-design` rather than `InternalsVisibleTo`.
