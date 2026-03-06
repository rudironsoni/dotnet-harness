#!/usr/bin/env bash
# shellcheck disable=SC2154,SC2250

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib/common.sh
source "$SCRIPT_DIR/../lib/common.sh"

REPO_ROOT_PATH="$REPO_ROOT" python3 - <<'PY'
import json
import os
import pathlib
import re
import sys

repo_root = pathlib.Path(os.environ["REPO_ROOT_PATH"])

skills = sum(1 for p in (repo_root / ".rulesync" / "skills").iterdir() if p.is_dir() and (p / "SKILL.md").exists())
subagents = len(list((repo_root / ".rulesync" / "subagents").glob("*.md")))
commands = len(list((repo_root / ".rulesync" / "commands").glob("*.md")))
mcp_servers = json.loads((repo_root / ".rulesync" / "mcp.json").read_text(encoding="utf-8")).get("mcpServers", {})
mcp_count = len(mcp_servers)
mcp_keys = sorted(mcp_servers.keys())

errors = []


def read_text(relative_path: str) -> str:
    return (repo_root / relative_path).read_text(encoding="utf-8")


def require_count(relative_path: str, pattern: str, expected: int, label: str) -> None:
    path = repo_root / relative_path
    if not path.exists():
        return
    text = read_text(relative_path)
    match = re.search(pattern, text)
    if not match:
        errors.append(f"{relative_path}: missing {label} pattern")
        return
    actual = int(match.group(1))
    if actual != expected:
        errors.append(f"{relative_path}: {label} mismatch (doc={actual}, source={expected})")


overview_docs = [
    ".rulesync/rules/overview.md",
    "AGENTS.md",
    "CLAUDE.md",
    "GEMINI.md",
    ".github/copilot-instructions.md",
]

for relative_path in overview_docs:
    require_count(relative_path, r"-\s*(\d+)\s+skills", skills, "skills count")
    require_count(relative_path, r"-\s*(\d+)\s+specialist agents/subagents", subagents, "subagents count")

readme_path = "README.md"
require_count(readme_path, r">\s*(\d+)\s+specialized skills", skills, "hero skills count")
require_count(readme_path, r"specialized skills\s*[·|]\s*(\d+)\s+expert subagents", subagents, "hero subagents count")
require_count(readme_path, r"expert subagents\s*[·|]\s*(\d+)\s+powerful commands", commands, "hero commands count")
require_count(readme_path, r"\|\s*\*\*Skills\*\*\s*\|\s*(\d+)\s*\|", skills, "table skills count")
require_count(readme_path, r"\|\s*\*\*Subagents\*\*\s*\|\s*(\d+)\s*\|", subagents, "table subagents count")
require_count(readme_path, r"\|\s*\*\*Commands\*\*\s*\|\s*(\d+)\s*\|", commands, "table commands count")
require_count(readme_path, r"\|\s*\*\*MCP Servers\*\*\s*\|\s*(\d+)\s*\|", mcp_count, "table MCP count")
require_count(readme_path, r"skills/\s+#\s*(\d+)\s+knowledge modules", skills, "architecture skills count")
require_count(readme_path, r"subagents/\s+#\s*(\d+)\s+specialized agents", subagents, "architecture subagents count")
require_count(readme_path, r"commands/\s+#\s*(\d+)\s+slash commands", commands, "architecture commands count")

inventory_text = read_text(readme_path)
inventory_match = re.search(r"MCP inventory \(source: `?\.rulesync/mcp\.json`?\):\s*(.+)", inventory_text)
if not inventory_match:
    errors.append("README.md: missing MCP inventory line")
else:
    names = re.findall(r"`([^`]+)`", inventory_match.group(1))
    if sorted(names) != mcp_keys:
        errors.append(
            "README.md: MCP inventory mismatch "
            f"(doc={sorted(names)}, source={mcp_keys})"
        )

if errors:
    print("Documentation contract validation failed:", file=sys.stderr)
    for err in errors:
        print(f"- {err}", file=sys.stderr)
    sys.exit(1)

print(
    "Documentation contract validation passed: "
    f"skills={skills}, subagents={subagents}, commands={commands}, mcp={mcp_count}"
)
PY
