# CLAUDE.md: Resonance of Steel (Project Protocol)

> **IMPORTANT: AI AGENT INSTRUCTION**
> This document is the **Source of Truth** for the Resonance of Steel project. Any LLM or AI agent working on this repository is **STRICTLY REQUIRED** to update this file immediately following any architectural changes, new system implementations, or shifts in design philosophy. Do not allow this document to become stale. Every session should begin by reading this file and end by refining it to reflect the current state of the prototype.

---

## 1. Project Overview
**Resonance of Steel** is a high-fidelity, 3D third-person competitive 1v1 fighting game prototype built in Godot 4. Unlike traditional "health-bar" fighters, it focuses on **Spatial Rhythm** and tactical resource management. The gameplay emphasizes weight, momentum, and postural integrity (Composure) over long-form combos. Players engage in high-stakes duels where positioning and frame-perfect timing dictate the flow of combat, culminating in "Deathblow" finishers once an opponent's defense is systematically dismantled.

---

## 2. Technical Architecture
The project utilizes a **Tri-Layer Deterministic Architecture** to ensure simulation consistency and future-proof the codebase for rollback netcode.

### A. Simulation Layer (`src/Simulation/`)
*   **Pure C#:** Zero dependencies on `Godot` namespaces.
*   **Deterministic Math:** Uses `Fixed64` via `FixedMathSharp` for all physics and logic calculations to avoid floating-point drift.
*   **State Management:** Powered by `Chickensoft.LogicBlocks`. Character logic is a Hierarchical State Machine (HSM) where states return transitions rather than mutating globally.

### B. Bridge Layer (`src/Bridge/`)
*   **The "Glue":** Standard Godot nodes (e.g., `CharacterBody3D`) that act as wrappers.
*   **Input Handling:** Polls hardware and maps it to Simulation-friendly command buffers.
*   **Physics Queries:** Executes manual `IntersectShape()` calls against the world to detect hits/collisions on specific frames, bypassing the non-deterministic signal-based physics engine.

### C. Presentation Layer (`src/Presentation/`)
*   **Visual-Only:** Nodes that read from the Simulation Layer but never write to it.
*   **Components:** `AnimationPlayer` (driven by simulation frame-data), HUD/UI elements, and VFX triggers.

---

## 3. Core Systems & Mechanics

### Combat Economy
| Metric | Function | Recovery/Cost |
| :--- | :--- | :--- |
| **Vitality** | "Lethality Regulator." If low, slows Composure recovery. | Static; does not regenerate. |
| **Composure** | Postural health. At 0, player is vulnerable to a Deathblow. | $R_{comp} = B_{rate} \times \frac{V_{curr}}{V_{max}}$ |
| **Momentum** | 8-segment resource for advanced maneuvers. | Gain: Forward movement, hits. Loss: Evasion, Shatter. |

### Movement & Staging
*   **Camera:** Shared 3D space rendered via dual `SubViewport` nodes for split-screen. Uses **Phantom Camera** for intelligent framing.
*   **Stages:** Defined by strict physical boundaries. Small (10m), Medium (20m), Large (35m).
*   **Match Rules:** 4 Lives per player; 210-second round timer. Tiebreaks decided by highest % Vitality.

---

## 4. Implementation Standards

### State Machine Protocol
All character actions must be implemented as discrete states within the `LogicBlocks` framework.
1.  **Coil:** The start-up/wind-up frames.
2.  **Active:** The window where `Bridge` queries for hit detection.
3.  **Recover:** The "end-lag" where the character is vulnerable.
4.  **Recoil:** Forced state upon being blocked or parried.

### Hit Detection Workflow
Do **not** use `Area3D` signals. Use the following sequence:
1.  Simulation triggers `RequestHitbox` event.
2.  Bridge performs `GetWorld3D().DirectSpaceState.IntersectShape()`.
3.  Results are passed back to Simulation for damage/stagger resolution.

---

## 5. Archetype Profiles
*   **Longsword:** Balanced reach and speed. Features **Flicks** (Momentum builders) and **Lunges** (Armor-granting finishers).
*   **Greatsword:** High-commitment, high-reward. Focuses on **Crush** and **Cleave** attacks that deal massive Composure damage.
*   **Shatter Modifier:** A universal mechanic costing 3.0 Momentum. When applied to any attack, it pierces through the opponent's "Parry" state, punishing overly defensive play.