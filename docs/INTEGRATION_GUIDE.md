# Integration Guide

This guide covers the maintained integration surfaces in this repository:

1. Runtime tool installation into a consumer repo
2. RuleSync installation and generation for agent targets
3. Prompt evidence, incident tracking, and eval artifact workflows
4. MCP prerequisites and validation

## Quick Start

### 1. Install the Runtime Tool

```bash
# Local tool manifest (recommended)
dotnet new tool-manifest
dotnet tool install Rudironsoni.DotNetAgentHarness
```

The installed command is available as `dotnet agent-harness`.

### 2. Bootstrap the Repo for Supported Agent Targets

```bash
dotnet agent-harness bootstrap \
  --targets claudecode,opencode,codexcli,geminicli,copilot,antigravity \
  --run-rulesync
```

Bootstrap writes:

- `.config/dotnet-tools.json`
- `rulesync.jsonc`
- `.dotnet-agent-harness/project-profile.json`
- `.dotnet-agent-harness/recommendations.json`
- `.dotnet-agent-harness/doctor-report.json`
- `.dotnet-agent-harness/bootstrap-report.json`

Expected generated target roots:

- `claudecode` -> `.claude/`
- `opencode` -> `.opencode/` and `AGENTS.md`
- `codexcli` -> `.codex/` and `AGENTS.md`
- `geminicli` -> `.gemini/` and `GEMINI.md`
- `copilot` -> `.github/agents/`, `.github/instructions/`, `.github/prompts/`, `.github/skills/`
- `antigravity` -> `.agent/`

Generated `dotnet-agent-harness:*` command files are intended to call the local runtime directly:

```bash
dotnet agent-harness <subcommand> ...
```

### 3. Build the Runtime Executables

The repository also ships two `.NET` runtimes used by maintainers and CI:

```bash
dotnet build src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj
dotnet build src/DotNetAgentHarness.Evals/DotNetAgentHarness.Evals.csproj
```

### 4. Run Repository Analysis

```bash
dotnet agent-harness analyze --format json
dotnet agent-harness recommend --format json
```

These commands build the repo profile that powers prompt preparation, validation, and recommendations.

## Prompt Assembly and Evidence

Use `prepare-message` to build a deterministic, repository-aware prompt bundle before implementation, review, or
planning:

```bash
dotnet agent-harness \
  prepare-message "Review the runtime validation path" \
  --target src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj \
  --platform antigravity \
  --write-evidence \
  --evidence-id review-runtime-validation \
  --format json
```

The output includes:

- resolved persona
- resolved target or ambiguity warning
- recommended skills and preferred subagent
- enriched request text
- four-layer prompt bundle
- rendered prompt for the selected platform
- optional saved evidence

Saved prompt artifacts live under:

```text
.dotnet-agent-harness/evidence/prepared-messages/
```

Compare two prompt bundles when reviewing changes to personas, tool policy, or request shaping:

```bash
dotnet agent-harness \
  compare-prompts review-runtime-validation review-runtime-validation-v2 --format json
```

## Incident Workflow

Incidents link prompt evidence and eval failures to a durable record under:

```text
.dotnet-agent-harness/incidents/
```

### Create an Incident from Prompt Evidence

```bash
dotnet agent-harness \
  incident add "Reviewer misrouted validation request" \
  --prompt-evidence review-runtime-validation \
  --severity high \
  --owner platform-team
```

### Create an Incident from an Eval Artifact

```bash
dotnet agent-harness \
  incident from-eval nightly-routing \
  --prompt-evidence review-runtime-validation \
  --incident-id nightly-routing-failure \
  --owner eval-bot
```

### Inspect Saved Incidents

```bash
dotnet agent-harness incident list
dotnet agent-harness incident show nightly-routing-failure
```

### Resolve and Close Incidents

Do not resolve or close an incident without a linked permanent regression case.

```bash
dotnet agent-harness \
  incident resolve nightly-routing-failure \
  --owner platform-team \
  --rationale "Added permanent regression coverage." \
  --regression-case routing-reviewer-001 \
  --regression-path tests/eval/cases/routing.yaml

dotnet agent-harness \
  incident close nightly-routing-failure \
  --owner release-manager \
  --rationale "Regression remains green in nightly and release gates." \
  --regression-case routing-reviewer-001
```

## Eval Artifacts and CI

The eval runner can emit machine-readable artifacts that feed the incident workflow:

```bash
dotnet run --project src/DotNetAgentHarness.Evals/DotNetAgentHarness.Evals.csproj -- \
  --cases tests/eval/cases/routing.yaml \
  --artifact-id nightly-routing \
  --gate nightly \
  --policy-profile strict \
  --prompt-evidence review-runtime-validation
```

Artifacts are written under:

```text
.dotnet-agent-harness/evidence/evals/
```

### CI Wrapper

Use the CI wrapper to preserve the eval exit code while still publishing an artifact and optionally creating an
incident:

```bash
DOTNET_AGENT_HARNESS_EVAL_GATE=nightly \
DOTNET_AGENT_HARNESS_EVAL_POLICY_PROFILE=strict \
DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID=nightly-routing \
DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID=review-runtime-validation \
DOTNET_AGENT_HARNESS_EVAL_CREATE_INCIDENT=true \
DOTNET_AGENT_HARNESS_EVAL_INCIDENT_ID=nightly-routing-failure \
DOTNET_AGENT_HARNESS_EVAL_INCIDENT_OWNER=ci-eval-bot \
bash scripts/ci/run_evals.sh --cases tests/eval/cases/routing.yaml
```

Supported CI environment variables:

- `DOTNET_AGENT_HARNESS_EVAL_DUMMY_MODE`
- `DOTNET_AGENT_HARNESS_EVAL_TRIALS`
- `DOTNET_AGENT_HARNESS_EVAL_GATE`
- `DOTNET_AGENT_HARNESS_EVAL_POLICY_PROFILE`
- `DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID`
- `DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID`
- `DOTNET_AGENT_HARNESS_EVAL_CREATE_INCIDENT`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_OWNER`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_SEVERITY`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_ID`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_NOTES`

Behavior:

- exit code `0`: all eval trials passed
- exit code `1`: one or more eval trials failed; auto-incident creation can still run
- exit code `2+`: configuration or runtime failure; no auto-incident should be assumed

## Tool Publishing

Maintainers publish the runtime tool with
[publish-dotnet-tool.yml](/home/rrj/src/github/rudironsoni/dotnet-harness-toolkit/.github/workflows/publish-dotnet-tool.yml).

That workflow:

1. packs `Rudironsoni.DotNetAgentHarness` using the semver tag version
2. smoke-installs the packed tool in a temporary repo
3. publishes to GitHub Packages
4. smoke-installs from GitHub Packages
5. optionally publishes to NuGet.org when `NUGET_API_KEY` is configured

## MCP Integration

The toolkit ships MCP definitions in `.rulesync/mcp.json`. Current inventory:

- `context7`
- `deepwiki`
- `github`
- `mcp-windbg`
- `microsoftdocs-mcp`
- `serena`

Typical prerequisites:

```bash
# Node.js for npx-based MCP servers
node --version
npx --version

# uv for Python-based MCP servers
uv --version
uvx --version
```

Validate the generated MCP configuration after `rulesync generate`:

```bash
jq empty .mcp.json && echo "MCP configuration is valid"
jq -r '.mcpServers | keys[]' .mcp.json
```

Set credentials when needed:

```bash
export GITHUB_TOKEN=your_github_personal_access_token
```

## Troubleshooting

| Issue                                            | Action                                                                                                     |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------------------------- |
| `Multiple root rulesync rules found`             | Ensure only one root overview rule exists in `.rulesync/rules/`.                                           |
| `incident from-eval` cannot find prompt evidence | Pass `--prompt-evidence <id>` explicitly or ensure the eval artifact stored `PromptEvidenceId`.            |
| CI eval fails with dummy mode                    | `CI=true` blocks dummy mode intentionally; set real provider credentials or run fixture mode locally only. |
| Repo fills with runtime state                    | `.dotnet-agent-harness/` is ignored by Git and should remain repo-local.                                   |
| MCP command missing                              | Install the required runtime (`node/npx`, `uv/uvx`) and re-run `rulesync generate`.                        |

## Next Steps

- Use `prepare-message --write-evidence` before high-risk review or implementation tasks.
- Feed saved prompt evidence ids into eval runs with `--prompt-evidence`.
- Enable `DOTNET_AGENT_HARNESS_EVAL_CREATE_INCIDENT=true` in nightly or release validation once the incident flow is
  part of your governance process.
