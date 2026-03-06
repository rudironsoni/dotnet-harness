#!/usr/bin/env bash
set -euo pipefail

: "${DOTNET_AGENT_HARNESS_EVAL_DUMMY_MODE:=false}"
: "${DOTNET_AGENT_HARNESS_EVAL_TRIALS:=3}"
: "${DOTNET_AGENT_HARNESS_EVAL_GATE:=pr}"
: "${DOTNET_AGENT_HARNESS_EVAL_POLICY_PROFILE:=balanced}"
default_artifact_id="ci-eval-$(date -u +%Y%m%d%H%M%S)"
: "${DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID:=${default_artifact_id}}"
: "${DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID:=}"
: "${DOTNET_AGENT_HARNESS_EVAL_CREATE_INCIDENT:=false}"
: "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_OWNER:=ci-eval-bot}"
: "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_SEVERITY:=medium}"
: "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_ID:=}"
: "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_NOTES:=}"

eval_args=(
  --dummy-mode "${DOTNET_AGENT_HARNESS_EVAL_DUMMY_MODE}"
  --trials "${DOTNET_AGENT_HARNESS_EVAL_TRIALS}"
  --gate "${DOTNET_AGENT_HARNESS_EVAL_GATE}"
  --policy-profile "${DOTNET_AGENT_HARNESS_EVAL_POLICY_PROFILE}"
  --artifact-id "${DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID}"
)

if [[ -n "${DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID}" ]]; then
  eval_args+=(--prompt-evidence "${DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID}")
fi

set +e
dotnet run --project src/DotNetAgentHarness.Evals/DotNetAgentHarness.Evals.csproj -- \
  "${eval_args[@]}" \
  "$@"
eval_exit=$?
set -e

if [[ "${eval_exit}" -eq 1 && "${DOTNET_AGENT_HARNESS_EVAL_CREATE_INCIDENT}" == "true" ]]; then
  incident_args=(
    incident from-eval "${DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID}"
    --owner "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_OWNER}"
    --severity "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_SEVERITY}"
    --format json
  )

  if [[ -n "${DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID}" ]]; then
    incident_args+=(--prompt-evidence "${DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID}")
  fi

  if [[ -n "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_ID}" ]]; then
    incident_args+=(--incident-id "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_ID}")
  fi

  if [[ -n "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_NOTES}" ]]; then
    incident_args+=(--notes "${DOTNET_AGENT_HARNESS_EVAL_INCIDENT_NOTES}")
  fi

  if ! dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- "${incident_args[@]}"; then
    echo "Auto-incident creation failed for eval artifact '${DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID}'." >&2
  fi
fi

exit "${eval_exit}"
