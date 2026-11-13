# Licences of dependencies and bundled analyzers

Research for issue #9. It checks Plan.md principle 7 ("no dependency on a library with a commercial licence; development-only tooling is judged on its own licence terms") and risk R8. Checked on 2026-11-13.

## Method

- **NuGet:** `licenseExpression` (or `licenseFile`) from the NuGet v3 catalog entry of the latest version of each package. Where the package ships a licence file instead of an SPDX expression, the `.nupkg` was downloaded and the file read.
- **Source repo:** GitHub's detected licence (`GET /repos/{repo}/license`) plus the commit history of the LICENSE file, to find any change.
- Package versions are the latest on nuget.org on 2026-11-13. Coding-standards analyzer versions are the ones pinned by `Opinionated.DotNet.CodingStandards` 0.0.21.

Verdicts: **OK** = permissive, no concern. **Caution** = acceptable but has a condition to track. **Blocker** = conflicts with principle 7 or with how this repo is developed.

## Summary

| Package | Version checked | Licence (SPDX) | Verdict |
|---|---|---|---|
| WireMock.Net | 2.31.0 | Apache-2.0 | OK |
| Microsoft.Playwright | 1.64.1 | MIT | OK |
| Testcontainers | 4.16.0 | MIT | OK (see note on container images) |
| Aspire (Aspire.Hosting, Aspire.Hosting.AppHost) | 13.6.5 | MIT | OK |
| PactNet | 5.0.1 | MIT | OK |
| Confluent.Kafka | 2.16.0 | Apache-2.0 (native librdkafka: BSD-2-Clause) | OK |
| Reqnroll | 3.3.4 | BSD-3-Clause | OK |
| xunit.v3 | 4.0.3 | Apache-2.0 | OK |
| TUnit | 1.84.0 | MIT | OK |
| NUnit | 5.0.0 | MIT | OK |
| Opinionated.DotNet.CodingStandards | 0.0.21 | MIT | OK itself, but it brings in SonarAnalyzer.CSharp |
| Meziantou.Analyzer | 3.0.322 (pinned), 3.0.339 latest | MIT | OK |
| Microsoft.CodeAnalysis.NetAnalyzers | 11.0.100 | MIT | OK |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | MIT | OK |
| StyleCop.Analyzers | 1.3.177-beta (pinned), 1.3.180 latest | MIT | OK |
| **SonarAnalyzer.CSharp** | 10.35.0.4138 (pinned), 10.36.0.15337 latest | **Sonar Source-Available License v1.0 (SSAL)**, not OSI-approved. No SPDX id is listed; the package ships a licence file instead. | **Blocker** for this repo's AI-agent workflow (see below) |

Only SonarAnalyzer.CSharp has had a real licence change. It moved from LGPL-3.0-only to SSAL v1.0 in November 2024. No other package has a recent or announced change. Every other LICENSE edit since 2024 only updated the copyright line.

## SonarAnalyzer.CSharp

### Facts

- The nuspec of 10.36.0.15337 declares `<license type="file">licenses\LICENSE.txt</license>`. That file is the **SONAR Source-Available License v1.0, last updated 13 November 2024**. The same applies to 10.35.0.4138, the version pinned by the coding-standards package. Source: the package on nuget.org (`https://api.nuget.org/v3-flatcontainer/sonaranalyzer.csharp/10.36.0.15337/sonaranalyzer.csharp.10.36.0.15337.nupkg`, `licenses/LICENSE.txt`).
- **The change:** commit `c8cb3a2e1d` ("NET-623 Update License.txt", 2024-11-20) in [SonarSource/sonar-dotnet](https://github.com/SonarSource/sonar-dotnet/commits/master/LICENSE.txt) replaced the LGPL-3.0 text with SSAL v1.0.
- **NuGet versions:** the last LGPL release is **9.32.0.97167** (catalog `licenseExpression` = `LGPL-3.0-only`). The first SSAL release is **10.3.0.106239** (Dec 2024, `licenseFile` = `LICENSE.txt`). The NuGet index has no 10.0 to 10.2.
- **What SSAL grants (s.2a, 2b):** a royalty-free copyright and patent licence "for any Non-competitive Purpose". The copyright grant covers reproducing, making derivative works, and distributing. The patent grant covers making, using and selling.
- **"Non-competitive Purpose" (s.1)** means any purpose **except**:
  - (a) providing to others a product or service with functionality the same as or substantially similar to SonarQube;
  - (b) Competing with SonarQube;
  - (c) "**employing, using, or engaging artificial intelligence technology that is not part of the Program to ingest, interpret, analyze, train on, or interact with the data provided by the Program, or to engage with the Program in any manner**."
- **If you distribute it (s.3):** you must make the source available and include the licence. Lamina never distributes the analyzer: it is a `developmentDependency`, and D15 keeps it out of user templates. So s.3 does not apply.
- **Breach (s.6):** all rights end if you breach a material term and do not fix it within a reasonable time. You must then stop using it.

### Assessment

- **Development-only use by humans:** acceptable. Lamina is not a SonarQube competitor, and it does not redistribute the analyzer. Under LGPL this would have been a clear OK.
- **Development with AI agents:** conflicts with exclusion (c). This repo is built by AI coding agents (AGENTS.md, `.claude/skills`). Those agents run `dotnet build` and read and act on analyzer diagnostics, including Sonar's `S`-rule warnings, which are errors here because warnings are treated as errors. That is plainly "AI technology ... [interacting] with the data provided by the Program". Agent work is outside the licence grant, so the repo has no licence for that use. The wording is broad and has not been tested. It is a legal-reading question, but the plain text is against us.
- **Plan.md is now wrong:** §589 says the coding-standards package is MIT. That is true of the package itself, but one of the analyzers it brings in is not under an open-source licence.
- **No opt-out today:** Opinionated.DotNet.CodingStandards 0.0.21 has no property to switch off Sonar. Its README and props/targets have opt-outs only for banned-API groups and similar settings, nothing for Sonar.

**Verdict: Blocker** while the repo is developed with AI agents. For human-only development it would only be a caution.

### Options (for the owner to decide; not researched further)

1. **Remove SonarAnalyzer.CSharp from Opinionated.DotNet.CodingStandards** (the same owner maintains it). Most of its rules overlap with NetAnalyzers and Meziantou. This is the cleanest fix, and it also protects other users of that package.
2. **Pin SonarAnalyzer.CSharp 9.32.0.97167 (LGPL-3.0-only)**. This would be a direct `PackageReference` that overrides the transitive version. It works, but the version is frozen and gets no fixes. It may also not support newer C# and Roslyn versions; that has not been verified.
3. **Suppress its assets in Lamina only.** Add a direct `<PackageReference Include="SonarAnalyzer.CSharp" ... ExcludeAssets="all" />` (or `analyzers`) next to the coding-standards package. This is a standard NuGet mechanism, but it has not been tested against this package.
4. Get a legal reading of exclusion (c). This is not practical for a personal OSS project.

## Other packages

All have permissive licences. The NuGet licence field matches the repo LICENSE file in each case.

- **WireMock.Net:** Apache-2.0. NuGet expression; [LICENSE](https://github.com/wiremock/WireMock.Net/blob/master/LICENSE) unchanged since 2017.
- **Microsoft.Playwright:** MIT. NuGet; [LICENSE](https://github.com/microsoft/playwright-dotnet/blob/main/LICENSE) unchanged since 2020. The browsers Playwright downloads at runtime have their own licences. They are test-time tools and are not shipped.
- **Testcontainers:** MIT. NuGet; [LICENSE](https://github.com/testcontainers/testcontainers-dotnet/blob/develop/LICENSE), copyright year updated 2026-01-03, licence unchanged. The images it starts are licensed separately; see the note below.
- **Aspire:** MIT. NuGet (Aspire.Hosting / Aspire.Hosting.AppHost 13.6.5); [LICENSE.TXT](https://github.com/microsoft/aspire/blob/main/LICENSE.TXT) unchanged since 2023.
- **PactNet:** MIT. NuGet 5.0.1 (published 2025-03-22, the latest stable); [LICENSE.txt](https://github.com/pact-foundation/pact-net/blob/master/LICENSE.txt). The native Pact FFI it wraps ([pact-reference](https://github.com/pact-foundation/pact-reference/blob/master/LICENSE)) is MIT.
- **Confluent.Kafka:** Apache-2.0. NuGet; [LICENSE](https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/LICENSE) unchanged since 2017. The native [librdkafka](https://github.com/confluentinc/librdkafka/blob/master/LICENSE) is BSD-2-Clause; GitHub reports NOASSERTION only because the file also lists third-party notices.
- **Reqnroll:** BSD-3-Clause. NuGet; [LICENSE](https://github.com/reqnroll/Reqnroll/blob/main/LICENSE), only the year updated in 2025.
- **xunit.v3:** Apache-2.0. NuGet; the [LICENSE](https://github.com/xunit/xunit/blob/main/LICENSE) says Apache-2.0, with one file adapted from MSBuild under MIT. That is why GitHub reports NOASSERTION.
- **TUnit:** MIT. NuGet; [LICENSE](https://github.com/thomhurst/TUnit/blob/main/LICENSE) unchanged since 2024.
- **NUnit:** MIT. NuGet 5.0.0; [LICENSE.txt](https://github.com/nunit/nunit/blob/main/LICENSE.txt), copyright line updated for v5 (2026-06-14), still MIT.
- **Meziantou.Analyzer:** MIT. NuGet; [LICENSE.txt](https://github.com/meziantou/Meziantou.Analyzer/blob/main/LICENSE.txt), copyright-line edit in 2025.
- **Microsoft.CodeAnalysis.NetAnalyzers:** MIT. NuGet; source is now in [dotnet/sdk](https://github.com/dotnet/sdk/blob/main/LICENSE.TXT) (MIT).
- **Microsoft.CodeAnalysis.BannedApiAnalyzers:** MIT. NuGet; source in [dotnet/roslyn](https://github.com/dotnet/roslyn/blob/main/License.txt) (MIT). The old `dotnet/roslyn-analyzers` repo is archived.
- **StyleCop.Analyzers:** MIT. NuGet; [LICENSE](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/LICENSE). The pinned version is still a `-beta`, which is a stability matter, not a licensing one.
- **Opinionated.DotNet.CodingStandards:** MIT. NuGet expression; [LICENCE.txt](https://github.com/LeeSanderson/OpinionatedDotNetCodingStandards/blob/main/LICENCE.txt).

## Adjacent finding: Redpanda (not in the issue's list)

D12 has the demo running Redpanda, presumably through Testcontainers. Redpanda is not a NuGet dependency, but it is not permissively licensed. The core is under the **Redpanda Business Source License 1.1** ([licenses/bsl.md](https://github.com/redpanda-data/redpanda/blob/dev/licenses/bsl.md)). Each release changes to Apache-2.0 four years after it is released. The Additional Use Grant allows any use except offering a commercial "Streaming or Queuing Service". Running it locally as a demo or test container is allowed. **Verdict: Caution.** It is source-available, not open source. If the demo should strictly follow principle 7, use the Apache Kafka image (`apache/kafka`, Apache-2.0) instead.

## Recommendations

1. Decide on SonarAnalyzer.CSharp before the coding-standards package is added. Option 1 above (drop it upstream) is the simplest.
2. Correct Plan.md §589: the package is MIT, but SonarAnalyzer.CSharp in it is SSAL v1.0. Record R8 as having occurred for Sonar (LGPL to SSAL, November 2024).
3. The R8 CI licence check should allowlist SPDX ids (MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause). It should fail on any package that has a `licenseFile` and no expression; that is how SSAL appears on NuGet.
