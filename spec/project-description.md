# Project Description

## Vision

Leontes is a **Proactive OS Partner**. It integrates into Windows as an ambient layer that monitors system events, understands application UI structurally, builds a knowledge graph of people/files/projects, and extends its own capabilities by writing new tools. Reachable via CLI (PC) and Signal (mobile).

## Core Problem

AI assistants are reactive, stateless, and locked to one interface. Workflows generate constant signals (downloads, calendar, clipboard, active windows) that an agent could act on — but no self-hosted solution connects these while remaining reachable from your phone and learning over time.

## Core Modules

### M1: The Sentinel (Proactive Engine)
Monitors OS events without user input: file system, clipboard, calendar, active window. Pattern rules trigger suggestions or actions. Delivered via CLI or Signal.

### M2: Structural Vision (Windows)
Windows UI Automation to read application UI as a structured element tree. The agent interacts via accessibility APIs — no screenshots, no simulated clicks.

### M3: Synapse Graph (Memory)
Knowledge graph (PostgreSQL + pgvector) linking People, Files, and Projects. Resolves contextual references: "send this to the lead dev" → person lookup from Git/email history.

### M4: Channels
- **CLI** — terminal on the host machine.
- **Signal** — E2E encrypted, primary mobile channel.

Both feed into one async processing loop sharing the Synapse Graph.

### M5: Tool Forge (Self-Extending Agent)
The agent writes, tests, and registers new tools at runtime. Flow: user asks for something with no matching tool (or agent detects a repeated pattern) → agent generates a tool class → compiles and runs a test → user approves → tool registered in the catalog. Usage tracked in Synapse Graph; unused tools pruned automatically.

### M6: Setup Wizard
Interactive CLI (`leontes init`) for first-run configuration: spins up PostgreSQL via Docker Compose, configures AI provider + API keys (stored in .NET User Secrets), walks through Signal bot registration, sets Sentinel watch folders, generates auth secrets.

## Post-MVP

Ghost Overlay (transparent system overlay), Voice I/O, Web Dashboard, The Vault (sandboxed execution in Docker/Micro-VM).

## Licensing & Monetization

**License:** AGPL-3.0 with commercial dual-licensing.

- **Free** — personal use, experimentation, research, non-commercial. Full-featured, no warranty.
- **Paid (commercial license)** — any company using Leontes to support its business, bundling it in a product, or redistributing it. Per-seat or per-organization pricing.

Source code is fully public. AGPL enforces this split: commercial users who don't want to open-source their own stack must buy the commercial license.

## Scope Boundaries

- **MVP:** Sentinel (all 4 inputs), Structural Vision, Synapse Graph, CLI + Signal, Tool Forge (user-triggered), Setup Wizard.
- **Post-MVP:** Ghost Overlay, voice, web dashboard, Vault, autonomous tool creation (agent-initiated).
- **Out of scope:** Multi-user, macOS/Linux Structural Vision.
