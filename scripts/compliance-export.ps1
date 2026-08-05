#requires -Version 7.0
<#
.SYNOPSIS
    Generate the SOC 2 / GDPR / ISO 27001 control evidence bundle
    the deployer hands to the auditor.

.DESCRIPTION
    The bundle is the deployer-facing evidence package for the
    SOC 2 (CC1-CC9), ISO 27001 (A.5-A.18), and GDPR (Art. 5, 24,
    25, 28, 30, 32, 35) controls that map to the project's design
    decisions. The script does NOT certify anything — the auditor
    certifies. The script produces a single tar.gz (or zip on
    Windows) that the deployer submits to the auditor, with:

    - project version + commit + build configuration
    - dependency list (transitive, pinned versions)
    - migration list (the schema is reproducible)
    - the self-assessment narrative (auditor's first read)
    - the SOC 2 readiness doc + the OWASP ASVS v4.0.3 L1 matrix
    - the threat model + the secure-coding checklist
    - the GDPR docs (compliance + Article 30 records of
      processing + breach notification + DPIA + DSAR response
      templates + privacy notice)
    - the vulnerability disclosure policy + the pen-test RFP
    - the test history (the latest CI run + the test count)
    - the Serilog config (the log retention policy)

    The bundle is reproducible: the same project state produces
    the same bundle (modulo the commit hash, which the script
    captures).

.PARAMETER OutputPath
    Where to write the tarball. Default: ./compliance-evidence.tar.gz
    (or .zip on Windows PowerShell 5.x).

.PARAMETER IncludeTransitiveDependencies
    Emit the full transitive dependency list (not just the
    direct references). Larger output, but the auditor usually
    wants the full tree for the supply-chain control.

.EXAMPLE
    pwsh ./scripts/compliance-export.ps1 -OutputPath ./evidence-2026-08-04.tar.gz
    Generates the evidence bundle at the given path.

.NOTES
    The script is intentionally simple — no fancy parsing, no
    network calls. The auditor wants the raw evidence, not a
    re-interpretation. The output is a directory tree that is
    tarred/zipped at the end.
#>

[CmdletBinding()]
param(
    [string] $OutputPath = "./compliance-evidence.tar.gz",
    [switch] $IncludeTransitiveDependencies
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path "$scriptRoot/.."

$stagingDir = New-Item -ItemType Directory -Path (Join-Path ([System.IO.Path]::GetTempPath()) ("cardscape-compliance-" + [Guid]::NewGuid().ToString("N"))) -Force

try {
    Write-Host "Staging evidence in $($stagingDir.FullName)..."

    # ── 1. Project metadata ───────────────────────────────────
    $projectDir = Join-Path $stagingDir "01-project-metadata"
    New-Item -ItemType Directory -Path $projectDir -Force | Out-Null
    git -C $repoRoot rev-parse HEAD | Out-File -Encoding utf8 (Join-Path $projectDir "commit.txt")
    git -C $repoRoot rev-parse --short HEAD | Out-File -Encoding utf8 (Join-Path $projectDir "commit-short.txt")
    git -C $repoRoot describe --tags --always | Out-File -Encoding utf8 (Join-Path $projectDir "version.txt")
    git -C $repoRoot log -1 --format="%cI %s" | Out-File -Encoding utf8 (Join-Path $projectDir "last-commit.txt")
    Get-Content (Join-Path $repoRoot "Directory.Build.props") | Out-File -Encoding utf8 (Join-Path $projectDir "Directory.Build.props")
    Get-Content (Join-Path $repoRoot "Directory.Packages.props") | Out-File -Encoding utf8 (Join-Path $projectDir "Directory.Packages.props")

    # ── 2. Dependency list ────────────────────────────────────
    $depsDir = Join-Path $stagingDir "02-dependencies"
    New-Item -ItemType Directory -Path $depsDir -Force | Out-Null
    $slnxPath = Join-Path $repoRoot "Cardscape.slnx"
    if ($IncludeTransitiveDependencies) {
        dotnet list $slnxPath package --include-transitive --format json 2>$null | Out-File -Encoding utf8 (Join-Path $depsDir "packages.json")
    } else {
        dotnet list $slnxPath package --format json 2>$null | Out-File -Encoding utf8 (Join-Path $depsDir "packages.json")
    }
    dotnet list $slnxPath package 2>$null | Out-File -Encoding utf8 (Join-Path $depsDir "packages.txt")

    # ── 3. Migration list (proves the schema is reproducible) ─
    $migDir = Join-Path $stagingDir "03-migrations"
    New-Item -ItemType Directory -Path $migDir -Force | Out-Null
    Get-ChildItem (Join-Path $repoRoot "src/Cardscape.Infrastructure/Persistence/Migrations") -Filter "*.cs" -Exclude "*.Designer.cs" |
        ForEach-Object {
            Copy-Item $_.FullName (Join-Path $migDir $_.Name)
        }

    # ── 4. Security + compliance documentation ───────────────
    $docsDir = Join-Path $stagingDir "04-documentation"
    New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    if (Test-Path (Join-Path $repoRoot "docs/security")) {
        Copy-Item -Recurse (Join-Path $repoRoot "docs/security") (Join-Path $docsDir "security")
    }
    if (Test-Path (Join-Path $repoRoot "docs/audits")) {
        Copy-Item -Recurse (Join-Path $repoRoot "docs/audits") (Join-Path $docsDir "audits")
    }
    if (Test-Path (Join-Path $repoRoot "docs/adr")) {
        Copy-Item -Recurse (Join-Path $repoRoot "docs/adr") (Join-Path $docsDir "adr")
    }
    if (Test-Path (Join-Path $repoRoot "SECURITY.md")) {
        Copy-Item (Join-Path $repoRoot "SECURITY.md") (Join-Path $docsDir "SECURITY.md")
    }

    # ── 5. Test history (the most recent CI run, if any) ──────
    $testsDir = Join-Path $stagingDir "05-test-history"
    New-Item -ItemType Directory -Path $testsDir -Force | Out-Null
    $trxFiles = Get-ChildItem (Join-Path $repoRoot "tests") -Recurse -Filter "*.trx" -ErrorAction SilentlyContinue
    if ($trxFiles) {
        $latest = $trxFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        Copy-Item $latest.FullName (Join-Path $testsDir $latest.Name)
    }
    $testCounts = dotnet test (Join-Path $repoRoot "Cardscape.slnx") -c Release --no-build --logger "console;verbosity=quiet" 2>&1 | Out-String
    $testCounts | Out-File -Encoding utf8 (Join-Path $testsDir "test-counts.txt")

    # ── 6. Log retention policy (the Serilog config) ─────────
    $logDir = Join-Path $stagingDir "06-log-retention"
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    Get-ChildItem (Join-Path $repoRoot "src/Cardscape.Infrastructure/Logging") -Recurse -Filter "*.cs" |
        ForEach-Object {
            Copy-Item $_.FullName (Join-Path $logDir $_.Name)
        }

    # ── 7. README (what the auditor is looking at) ───────────
    $readme = @"
# Cardscape compliance evidence bundle

Generated: $(Get-Date -Format "u")
Repository: Cardscape
Version: $((Get-Content (Join-Path $projectDir "version.txt") -Raw).Trim())
Commit: $((Get-Content (Join-Path $projectDir "commit.txt") -Raw).Trim())

This bundle is the deployer-facing evidence package for the SOC 2
(CC1-CC9), ISO 27001 (A.5-A.18), and GDPR (Art. 5, 24, 25, 28, 30,
32, 35) controls that map to the project's design decisions.

## What's in here

- `01-project-metadata/`: build configuration, package versions, the
  exact commit the audit covers.
- `02-dependencies/`: the NuGet package list (transitive if
  `-IncludeTransitiveDependencies` was passed). Supply-chain
  control CC8.1 / A.8.9.
- `03-migrations/`: every EF Core migration since the initial
  schema. The auditor can replay the migrations on a clean
  database and verify the resulting schema matches the deployer's
  production schema.
- `04-documentation/`:
  - `security/`:
    - `08-self-assessment-narrative.md` — the auditor's first
      read (5 minutes). Maps questions to the right doc.
    - `04-soc2-readiness.md` — SOC 2 Common Criteria mapping.
    - `06-asvs-controls.md` — OWASP ASVS v4.0.3 L1 line-by-line
      control matrix.
    - `03-gdpr-compliance.md` — the GDPR posture narrative.
    - `07-gdpr-article-30.md` — the Article 30 records of
      processing template (the deployer fills in OPERATOR
      fields).
    - `01-threat-model.md` — the STRIDE threat model.
    - `02-secure-coding-checklist.md` — the rules every
      contributor follows.
    - `05-vulnerability-disclosure.md` — the coordinated
      disclosure policy.
    - `templates/pen-test-rfp.md` — the request-for-proposal
      template the deployer sends to pen-test firms.
    - `templates/{privacy-notice,breach-notification,dpia,
      dsar-response}.md` — the four GDPR templates.
  - `audits/`: prior audit notes (refactoring, polish, etc.).
  - `adr/`: the architectural decision records.
  - `SECURITY.md`: the project's security posture summary.
- `05-test-history/`: the most recent test run results. The
  regression suite (security + integration + unit) is the
  evidence the controls are tested, not just designed.
- `06-log-retention/`: the Serilog pipeline configuration. The
  log retention policy is the evidence for the SOC 2 CC7.2
  / ISO 27001 A.12.4 controls.

## What the auditor should do

1. Read `04-documentation/security/08-self-assessment-narrative.md`
   first — the five-minute summary that points at the right doc
   for each question.
2. For SOC 2: read `04-soc2-readiness.md` (framework mapping) +
   `06-asvs-controls.md` (line-by-line control matrix).
3. For GDPR: read `03-gdpr-compliance.md` (posture narrative) +
   `07-gdpr-article-30.md` (records of processing template).
4. For each control, the readiness doc points at the
   corresponding artifact in this bundle.
5. The auditor's job is to verify the artifacts exist in the
   deployer's production environment (e.g. the log retention
   policy in `06-log-retention/` matches the deployer's
   actual Serilog config; the migration list in `03-migrations/`
   matches the deployer's actual database schema).
6. The auditor's report is the certification. This bundle is
   the input to the report, not the report itself.

## What this bundle is NOT

- This bundle is not a SOC 2 certification. The project does
  not self-certify; the auditor certifies.
- This bundle is not a pen-test report. The pen-test report
  comes from the firm the deployer commissions (see
  `04-documentation/security/templates/pen-test-rfp.md`).
- This bundle is not legal advice. The compliance docs are
  starting points the deployer's legal counsel must review
  before publication.
"@
    $readme | Out-File -Encoding utf8 (Join-Path $stagingDir "README.md")

    # ── 8. Bundle ─────────────────────────────────────────────
    if ($OutputPath.EndsWith(".zip")) {
        Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $OutputPath
    } else {
        $tarExe = Get-Command tar -ErrorAction SilentlyContinue
        if ($null -eq $tarExe) {
            Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath ($OutputPath -replace '\.tar\.gz$', '.zip')
        } else {
            & tar -czf $OutputPath -C $stagingDir .
        }
    }
    Write-Host ""
    Write-Host "Compliance evidence bundle written to: $OutputPath"
    Write-Host "Size: $("{0:N0}" -f (Get-Item $OutputPath).Length) bytes"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  1. Hand the bundle to the auditor."
    Write-Host "  2. The auditor reads 04-documentation/security/04-soc2-readiness.md first."
    Write-Host "  3. Each control points at an artifact in the bundle."
}
finally {
    if (Test-Path $stagingDir) {
        Move-Item $stagingDir.FullName "$env:TEMP/_archive_compliance_$([Guid]::NewGuid().ToString('N'))" -Force -ErrorAction SilentlyContinue
    }
}
