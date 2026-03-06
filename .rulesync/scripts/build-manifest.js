#!/usr/bin/env node

/**
 * Build Skill Manifest
 *
 * Generates .rulesync/manifest/skill-manifest.json from all skill frontmatter.
 * Run: node scripts/build-manifest.js [--watch] [--validate]
 */

const fs = require('fs').promises;
const path = require('path');
const yaml = require('js-yaml');

const SKILLS_DIR = path.join(__dirname, '..', 'skills');
const MANIFEST_DIR = path.join(__dirname, '..', 'manifest');
const MANIFEST_FILE = path.join(MANIFEST_DIR, 'skill-manifest.json');

/**
 * Parse frontmatter from markdown file
 */
function parseFrontmatter(content) {
  const match = content.match(/^---\s*\n([\s\S]*?)\n---\s*\n/);
  if (!match) return null;

  try {
    return yaml.load(match[1]);
  } catch (e) {
    console.error(`Failed to parse frontmatter: ${e.message}`);
    return null;
  }
}

/**
 * Extract skill references from content
 */
function extractSkillReferences(content) {
  const references = new Set();
  const regex = /\[skill:([a-z0-9-]+)\]/g;

  while (true) {
    const match = regex.exec(content);
    if (match === null) break;
    references.add(match[1]);
  }

  return Array.from(references);
}

/**
 * Build manifest from skills directory
 */
async function buildManifest() {
  const skills = {};
  const errors = [];

  try {
    const entries = await fs.readdir(SKILLS_DIR, { withFileTypes: true });

    for (const entry of entries) {
      if (!entry.isDirectory()) continue;

      const skillName = entry.name;
      const skillFile = path.join(SKILLS_DIR, skillName, 'SKILL.md');

      try {
        const content = await fs.readFile(skillFile, 'utf8');
        const frontmatter = parseFrontmatter(content);

        if (!frontmatter) {
          errors.push({ skill: skillName, error: 'No frontmatter found' });
          continue;
        }

        const lines = content.split('\n');
        const references = extractSkillReferences(content);

        // Infer dependencies from skill references if not explicitly declared
        const declaredDeps = frontmatter.depends_on || [];
        const inferredDeps = references.filter(
          ref => ref !== skillName && !declaredDeps.includes(ref)
        );

        // Detect platforms from frontmatter
        const platforms = [];
        if (frontmatter.claudecode) platforms.push('claudecode');
        if (frontmatter.opencode) platforms.push('opencode');
        if (frontmatter.copilot) platforms.push('copilot');
        if (frontmatter.codexcli) platforms.push('codexcli');
        if (frontmatter.geminicli) platforms.push('geminicli');
        if (frontmatter.antigravity) platforms.push('antigravity');

        skills[skillName] = {
          name: frontmatter.name || skillName,
          version: frontmatter.version || '0.0.1',
          description: frontmatter.description || '',
          tags: frontmatter.tags || [],
          depends_on: declaredDeps,
          inferred_dependencies: inferredDeps,
          optional: frontmatter.optional || [],
          conflicts_with: frontmatter.conflicts_with || [],
          file_path: `.rulesync/skills/${skillName}/SKILL.md`,
          line_count: lines.length,
          platforms: platforms.length > 0 ? platforms : ['*'],
        };
      } catch (e) {
        if (e.code === 'ENOENT') {
          errors.push({ skill: skillName, error: 'SKILL.md not found' });
        } else {
          errors.push({ skill: skillName, error: e.message });
        }
      }
    }

    // Detect circular dependencies
    const cycles = detectCircularDependencies(skills);

    // Detect version conflicts
    const conflicts = detectConflicts(skills);

    const manifest = {
      version: '1.0.0',
      generated_at: new Date().toISOString(),
      stats: {
        total_skills: Object.keys(skills).length,
        with_dependencies: Object.values(skills).filter(s => s.depends_on.length > 0).length,
        with_conflicts: Object.values(skills).filter(s => s.conflicts_with.length > 0).length,
        errors: errors.length,
        circular_dependencies: cycles.length,
        version_conflicts: conflicts.length,
      },
      errors: errors.length > 0 ? errors : undefined,
      circular_dependencies: cycles.length > 0 ? cycles : undefined,
      version_conflicts: conflicts.length > 0 ? conflicts : undefined,
      skills,
    };

    // Write manifest
    await fs.mkdir(MANIFEST_DIR, { recursive: true });
    await fs.writeFile(MANIFEST_FILE, JSON.stringify(manifest, null, 2));

    console.log(`✓ Manifest built: ${MANIFEST_FILE}`);
    console.log(`  - Total skills: ${manifest.stats.total_skills}`);
    console.log(`  - With dependencies: ${manifest.stats.with_dependencies}`);
    console.log(`  - With conflicts: ${manifest.stats.with_conflicts}`);

    if (errors.length > 0) {
      console.log(`\n⚠ ${errors.length} errors:`);
      errors.forEach(e => {
        console.log(`  - ${e.skill}: ${e.error}`);
      });
    }

    if (cycles.length > 0) {
      console.log(`\n⚠ ${cycles.length} circular dependencies detected:`);
      cycles.forEach(c => {
        console.log(`  - ${c.join(' → ')}`);
      });
    }

    if (conflicts.length > 0) {
      console.log(`\n⚠ ${conflicts.length} version conflicts:`);
      conflicts.forEach(c => {
        console.log(`  - ${c.skill} conflicts with: ${c.conflicts.join(', ')}`);
      });
    }

    return manifest;
  } catch (e) {
    console.error(`✗ Failed to build manifest: ${e.message}`);
    process.exit(1);
  }
}

/**
 * Detect circular dependencies using DFS
 */
function detectCircularDependencies(skills) {
  const cycles = [];
  const visited = new Set();
  const recStack = new Set();

  function dfs(skillName, path) {
    if (recStack.has(skillName)) {
      // Found cycle
      const cycleStart = path.indexOf(skillName);
      cycles.push([...path.slice(cycleStart), skillName]);
      return;
    }

    if (visited.has(skillName)) return;

    visited.add(skillName);
    recStack.add(skillName);
    path.push(skillName);

    const skill = skills[skillName];
    if (skill && skill.depends_on) {
      for (const dep of skill.depends_on) {
        dfs(dep, [...path]);
      }
    }

    recStack.delete(skillName);
  }

  for (const skillName of Object.keys(skills)) {
    if (!visited.has(skillName)) {
      dfs(skillName, []);
    }
  }

  return cycles;
}

/**
 * Detect version conflicts
 */
function detectConflicts(skills) {
  const conflicts = [];
  const skillNames = Object.keys(skills);

  for (const skillName of skillNames) {
    const skill = skills[skillName];

    // Check declared conflicts exist
    for (const conflict of skill.conflicts_with || []) {
      if (!skillNames.includes(conflict)) {
        conflicts.push({
          skill: skillName,
          type: 'missing_conflict_target',
          conflicts: [conflict],
        });
      }
    }

    // Check mutual conflicts
    for (const otherSkill of skillNames) {
      if (otherSkill === skillName) continue;

      const other = skills[otherSkill];
      const skillConflictsOther = skill.conflicts_with?.includes(otherSkill);
      const otherConflictsSkill = other.conflicts_with?.includes(skillName);

      if (skillConflictsOther && !otherConflictsSkill) {
        conflicts.push({
          skill: skillName,
          type: 'asymmetric_conflict',
          conflicts: [otherSkill],
          message: `${skillName} conflicts with ${otherSkill} but not vice versa`,
        });
      }
    }
  }

  return conflicts;
}

/**
 * Validate manifest against schema
 */
async function validateManifest(manifest) {
  // Basic validation
  const required = ['version', 'generated_at', 'skills'];
  for (const field of required) {
    if (!(field in manifest)) {
      throw new Error(`Missing required field: ${field}`);
    }
  }

  // Validate version format
  if (!/^\d+\.\d+\.\d+$/.test(manifest.version)) {
    throw new Error(`Invalid manifest version: ${manifest.version}`);
  }

  // Validate timestamp
  if (isNaN(Date.parse(manifest.generated_at))) {
    throw new Error(`Invalid generated_at timestamp: ${manifest.generated_at}`);
  }

  console.log('✓ Manifest validation passed');
  return true;
}

// CLI
const args = process.argv.slice(2);

if (args.includes('--validate')) {
  fs.readFile(MANIFEST_FILE, 'utf8')
    .then(JSON.parse)
    .then(validateManifest)
    .catch(e => {
      console.error(`✗ Validation failed: ${e.message}`);
      process.exit(1);
    });
} else if (args.includes('--watch')) {
  console.log('Watching for changes...');
  const { watch } = require('fs');

  watch(SKILLS_DIR, { recursive: true }, (eventType, filename) => {
    if (filename?.endsWith('.md')) {
      console.log(`\nChange detected: ${filename}`);
      buildManifest();
    }
  });
} else {
  buildManifest();
}
