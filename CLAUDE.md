# Agentic SDLC and Spec-Driven Development

Kiro-style Spec-Driven Development on an agentic SDLC

## Project Context

### Paths
- Steering: `.kiro/steering/`
- Specs: `.kiro/specs/`
- Unity project: `D:\Personal\Repositries\RealtimeAvatarController\RealtimeAvatarController`
- Unity Editor: `C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe` (version pinned by `ProjectSettings/ProjectVersion.txt`)

### Unity Editor Launch (authorized)
- When Unity Editor is closed, Claude is allowed to relaunch it from CLI so Unity MCP can reconnect.
- Command (background, detached):
  ```bash
  "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -projectPath "D:\Personal\Repositries\RealtimeAvatarController\RealtimeAvatarController"
  ```
- Always launch in the background (`run_in_background: true`) — the process blocks.
- Before launching, check whether Unity is already running (`tasklist | grep -i Unity.exe` or equivalent) to avoid a duplicate instance.
- Unity MCP reconnection takes several seconds after Editor fully loads — retry MCP calls with a short delay.

### Steering vs Specification

**Steering** (`.kiro/steering/`) - Guide AI with project-wide rules and context
**Specs** (`.kiro/specs/`) - Formalize development process for individual features

### Active Specifications
- Check `.kiro/specs/` for active specifications
- Use `/kiro:spec-status [feature-name]` to check progress

## Development Guidelines
- Think in English, generate responses in Japanese. All Markdown content written to project files (e.g., requirements.md, design.md, tasks.md, research.md, validation reports) MUST be written in the target language configured for this specification (see spec.json.language).

## Minimal Workflow
- Phase 0 (optional): `/kiro:steering`, `/kiro:steering-custom`
- Phase 1 (Specification):
  - `/kiro:spec-init "description"`
  - `/kiro:spec-requirements {feature}`
  - `/kiro:validate-gap {feature}` (optional: for existing codebase)
  - `/kiro:spec-design {feature} [-y]`
  - `/kiro:validate-design {feature}` (optional: design review)
  - `/kiro:spec-tasks {feature} [-y]`
- Phase 2 (Implementation): `/kiro:spec-impl {feature} [tasks]`
  - `/kiro:validate-impl {feature}` (optional: after implementation)
- Progress check: `/kiro:spec-status {feature}` (use anytime)

## Development Rules
- 3-phase approval workflow: Requirements → Design → Tasks → Implementation
- Human review required each phase; use `-y` only for intentional fast-track
- Keep steering current and verify alignment with `/kiro:spec-status`
- Follow the user's instructions precisely, and within that scope act autonomously: gather the necessary context and complete the requested work end-to-end in this run, asking questions only when essential information is missing or the instructions are critically ambiguous.

## Git / Commit Policy
- Commits are handled automatically by a repository hook. **Do NOT propose, suggest, or execute `git commit` unless the user explicitly asks.** Assume the hook has already captured recent changes.

## Steering Configuration
- Load entire `.kiro/steering/` as project memory
- Default files: `product.md`, `tech.md`, `structure.md`
- Custom files are supported (managed via `/kiro:steering-custom`)
