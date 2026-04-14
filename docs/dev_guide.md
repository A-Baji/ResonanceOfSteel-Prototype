# Resonance of Steel — Developer Guide

> This document describes the architecture, code logic, and file purposes for the Resonance of Steel prototype. It is the developer-facing complement to `CLAUDE.md` (the AI agent source of truth). Keep this file up to date when code changes occur.

---

## 1. Architecture Overview

The codebase uses a **Simulation-Observer** pattern with three strict layers. Dependencies flow inward only — outer layers may reference inner layers, but never the reverse.

```
┌─────────────────────────────────────────────┐
│         Presentation Layer (Observer)        │
│   Reads Bridge state. Never writes.         │
├─────────────────────────────────────────────┤
│            Bridge Layer (Conduit)            │
│   Polls input → calls Tick() → applies      │
│   physics → emits signals.                  │
│   Contains ZERO combat logic.               │
├─────────────────────────────────────────────┤
│        Simulation Layer (Pure C#)           │
│   NO Godot imports. Fixed64 math only.      │
│   Deterministic. Serializable.              │
└─────────────────────────────────────────────┘
```

### Why This Split

The simulation layer must be completely Godot-independent to support Phase 2 rollback netcode. Rollback requires:
1. Serializing the full game state to a byte array (snapshot)
2. Restoring a previous snapshot
3. Re-simulating N frames forward with corrected inputs

If any simulation state lived inside Godot Nodes, serialization/restore would require engine-level workarounds. The pure-C# simulation avoids this entirely.

---

## 2. Simulation Layer (`src/simulation/`)

All files in this directory are **pure C#** — no `using Godot` permitted. All numeric combat math uses `Fixed64` from FixedMathSharp for determinism (no `float`/`double`).

### PlayerSimulation.cs — Core State Machine

The central class. Owns all combat state for one player. Updated exactly once per physics frame via `Tick(PlayerInput)`.

**Key responsibilities:**
- State machine dispatch (`ProcessState` → per-state handlers)
- Input buffer management (enqueue → consume → age)
- Shatter window tracking (frame counter since last block press)
- Premature press penalty tracking (shared counter for whiffed parries/shatters)
- Combat event handlers (called by Bridge after hit resolution)
- Per-tick output flags (`HitboxActive`, `ArmorActive`, `CurrentTier`, `LastEvent`)

**State machine pattern:** Uses `private object _state` with C# record types (`Idle`, `Moving`, `Coil`, `Swing`, etc.) and pattern matching in a `switch` expression. Each state handler returns the next state. Frame-counted states use `record with { FramesLeft = framesLeft }` for immutable updates.

> **Phase 2 note:** The `object` + `switch` pattern will be replaced by Chickensoft LogicBlocks (already imported as NuGet package) for compile-time exhaustiveness, hierarchical states, and native serialization.

**Tick execution order:**
1. Reset per-tick outputs
2. Sync position from Bridge
3. Track Shatter window (block press recency)
4. Composure recovery tick
5. Frame Advantage Stack decay tick
6. Right-of-Way Momentum generation (if moving toward opponent within 60° cone, in Idle/Moving/Blocking)
7. Enqueue new inputs into buffer
8. Consume highest-priority buffered input (only in Idle/Moving — inputs stay in buffer during action-locked states)
9. Age buffer entries (decrement TTLs, remove expired)
10. Dispatch to state-specific processor

**Actionable states:** `Idle` and `Moving` share `ProcessActionableInput()` which handles all input types (block/attack/dodge/jump/held-block). This prevents input drops by checking actions before movement fallback.

**Action commitment:** All states except Idle, Moving, and Blocking are action-locked. During locked states, the input buffer does NOT consume entries — inputs remain buffered and expire via TTL. This means early inputs pressed near the end of an animation are preserved and consumed when the player returns to an actionable state. Recovery no longer accepts block-cancel.

### PlayerStates.cs — State Records

Defines all state types in `ResonanceOfSteel.Simulation.States`:

| Record | Fields | Notes |
|--------|--------|-------|
| `Idle` | — | Default grounded state |
| `Moving` | `IsRunning` | Walking or running |
| `Coil` | `Tier`, `FramesLeft` | Attack wind-up |
| `Swing` | `Tier`, `FramesLeft` | Active hitbox phase |
| `Recovery` | `FramesLeft`, `Tier` | Post-swing cooldown |
| `Blocking` | — | Standard block (held) |
| `Parrying` | `FramesLeft` | Active parry window |
| `Dodging` | `StartupLeft`, `ActiveLeft`, `RecoveryLeft` | Three-phase evasion (startup→active→recovery) |
| `Jumping` | `StartupLeft`, `ActiveLeft`, `RecoveryLeft` | Three-phase jump (startup→active→recovery, ground check during active) |
| `Staggered` | `FramesLeft` | Hit stun |
| `Deathblow` | — | Terminal execution state |

### EconomyHandler.cs — Resource Management

Manages the three combat resources:
- **Momentum** (0–8): Hidden resource. Costs: Parry 0.5, Dodge 1.5, Jump 1.0, Shatter 3.0. Generated by walking/running toward opponent and landing hits. Dodge and Jump are **always available** regardless of Momentum — insufficient Momentum degrades evasion frames (more startup, fewer active) rather than blocking the action.
- **Composure** (0–1): Deathblow gauge. Sekiro-inspired recovery: paused for `ComposureRecoveryCooldownFrames` (90 = 1.5s) after composure damage, then recovers at `B_rate × V²` (quadratic vitality scaling). Recovery stops at Terminal vitality.
- **Vitality** (0–1): Lethality regulator. Reaching 0 makes deathblow inevitable.

Also tracks **Frame Advantage Stacks** (integer, no cap). +1 per Perfect Parry. Each stack provides `FrameAdvantageOffset` (1 frame) of Coil reduction on the next counter-attack, consuming all stacks. Reset on: non-parried hit received (blocked or unblocked). Gradual decay: after `StackDecayDelayFrames` (180 = 3s) of no parry, stacks lose 1 every `StackDecayIntervalFrames` (60 = 1s).

**Derived states:**
- `IsFatigued`: Momentum ≤ 0 — disables Parry and Tier 3 armor; degrades Dodge/Jump frames
- `IsTerminal`: Vitality < threshold — halts Composure recovery
- `IsDeathblowVulnerable`: Composure ≥ 1 or Vitality ≤ 0

### EconomyConstants.cs — Balance Configuration

A `sealed record` with all balance parameters (economy values, frame constants, chip damage multiplier, stack decay timings). Constructed once at player initialization from `[Export]` values on `PlayerBridge`. Immutable after construction.

`Defaults` static property provides spec-defined baseline values for unit testing or non-inspector contexts.

### InputBuffer.cs — TTL Priority Queue

Buffers edge-detected button presses with a configurable time-to-live (default 6 frames). Prevents input drops during non-actionable states.

**Mechanics:**
- `Add()`: Enqueues action with full TTL; rejects duplicates
- `Consume()`: Returns highest-priority buffered action, removes it
- `Tick()`: Decrements all TTLs, removes expired entries
- **Priority order:** BlockParry > Attack > Dodge > Jump

**Execution order in PlayerSimulation.Tick:** Enqueue → Consume → Tick. This ensures freshly-added inputs are immediately available for consumption before aging.

### PlayerInput.cs — Per-Frame Input State

A `readonly struct` containing the complete input state sampled once per physics frame. `MoveX`/`MoveZ` are **world-space direction** (camera-transformed and normalized in `BuildPlayerInput`), not raw stick axes:
- Stick axes (`MoveX`, `MoveZ`) as `Fixed64`
- Edge-detected presses (`AttackJustPressed`, `BlockParryJustPressed`, etc.)
- Held states (`RunHeld`, `BlockParryHeld`)
- Modifier tier, grounded flag, own/opponent positions

### MoveData.cs — Per-Move Frame/Damage Data

A `readonly struct` bundling all data for a single attack move: Coil/Swing/Recovery frame counts, stagger frames, knockback distance, vitality/composure multipliers. Returned by `IArchetypeData.GetMoveData(tier)`.

### Enum Files

- **AttackTier.cs**: `Light`, `Standard`, `Heavy`, `Super` — maps to modifier buttons
- **PlayerInputAction.cs**: `None`, `Attack`, `BlockParry`, `Dodge`, `Jump` — buffer entries
- **ArchetypeType.cs**: `Longsword`, `Greatsword` — archetype selector
- **CombatEvent.cs**: Events emitted per-tick for the Presentation layer to observe

### Archetypes (`src/simulation/archetypes/`)

- **IArchetypeData**: Single method `GetMoveData(AttackTier) → MoveData`
- **LongswordData / GreatswordData**: Sealed singletons (`static readonly Instance`, private constructor). Return hardcoded `MoveData` per tier via exhaustive `switch` expressions. The `_ =>` fallback recursively calls `GetMoveData(Standard)` as a safety net.

---

## 3. Bridge Layer (`src/bridge/`)

Godot-aware conduit. Translates between engine and simulation. All classes are `sealed partial` (Godot source generators require `partial`).

### PlayerBridge.cs — Main Conduit

`CharacterBody3D` subclass. The primary per-player node.

**Responsibilities:**
- Exposes all economy/frame/setup constants as `[Export]` properties (organized by `[ExportGroup]`) for inspector-based playtesting
- Samples raw hardware input → transforms to world-space → constructs `PlayerInput` struct
- Calls `_sim.Tick(input)` exactly once per tick phase
- Applies movement via `MoveAndSlide()` with decaying knockback velocity blend
- Auto-faces opponent via yaw snap
- Manages boundary pushback on wall collision
- Delegates hitbox processing to `HitboxManager`
- Emits Godot signals for combat events (polled from `_sim.LastEvent` + fatigue edge detection)
- Provides public accessor methods for HUD/HitboxManager to read simulation state

**Two-pass execution:** `_PhysicsProcess` is disabled when `SetCoordinatorDriven()` is called. Instead, `GameCoordinator` drives `TickPhase(delta)` (input/simulation/movement) and `ResolvePhase()` (hitbox queries/combat events) as separate calls, ensuring both players tick before any resolution.

**Knockback:** Uses a `_knockbackVelocity` field that decays at 0.75× per frame, blended into `Velocity` during `ApplyMovement`. Produces smooth pushback over multiple frames instead of instant teleport. While knockback velocity is significant (> 1.0 m/s), input movement is suppressed to prevent players from walking through pushback.

**Movement priority in `ApplyMovement`:**
1. Deathblow/Staggered → complete halt (velocity zeroed)
2. Action-locked (Coil/Swing/Recovery/Parrying/Dodging/Jumping) → slide-to-stop at 0.85× friction per frame
3. Active knockback (non-locked state, e.g. blocking) → suppress input movement
4. Normal input → direction × walk/run speed
5. No input → stop

Knockback velocity is overlaid on top of all branches and decays independently.

**Action name caching:** Input action strings (e.g., `"attack"`, `"attack_p2"`) are built once via `CacheActionNames()`, deferred to the first `_PhysicsProcess` call to avoid race conditions with `PlayerIndex` assignment.

**Modifier tier logic:** At input sampling time, the currently-held modifier button determines `AttackTier`. If none held → `Standard`. Multiple modifiers → last-checked wins (Super > Heavy > Light due to `if` ordering).

### HitboxManager.cs — Manual Hit Detection & Combat Resolution

`Node3D` subclass. Runs `PhysicsDirectSpaceState3D.IntersectShape()` queries each frame during active Swing states.

**Why combat resolution lives here (not Simulation):** Resolution requires the hit/miss boolean from Godot's physics engine — an inherently engine-dependent query. All resulting state mutations flow back through `PlayerSimulation`'s public API (`OnHitReceived`, `OnClash`, etc.), keeping the Simulation as source of truth.

**Hit resolution order (within `ResolveHit`):**
1. **Clash detection** — Both swinging, same tier → mutual `NotifyClash()`, both enter Recovery, both receive tier-based knockback
2. **Armor trade** — Defender in Tier 3 Swing with active armor → 1.5× vitality damage, no stagger
3. **Parry + Shatter** — Defender is Parrying AND attacker is in shatter window → Shatter breaks parry, full damage
4. **Parry (no Shatter)** — Defender is Parrying → `NotifyParrySuccess()`, no damage
5. **Shatter whiff** — Attacker is in shatter window but defender is NOT Parrying → no damage, no momentum reward; attacker pays 3.0 Momentum + extra recovery frames
6. **Deathblow execution** — Defender is in Deathblow state → full damage + `NotifyDeathblowTriggered()`
7. **Standard hit/block** — Normal damage resolution; block knockback (T1+), stagger knockback (20% of block knockback via `StaggerKnockbackMultiplier`) for unblocked hits

**Physics query:** Uses cached `PhysicsShapeQueryParameters3D` instance (lazy-initialized alongside the `Exclude` RID list) to avoid per-frame heap allocation. `Shape` and `Transform` are updated each frame; `CollisionMask`, `CollideWithAreas`, `CollideWithBodies`, and `Exclude` are set once. `HurtboxLayer = 4` as collision mask. Shape comes from `IArchetypeVisuals.GetHitboxShape()`.

**Single-hit guard:** `_hitRegisteredThisSwing` prevents multiple hits per swing phase. Reset when hitbox deactivates.

### RoundManager.cs — Match Flow

Manages lives (default 4), round timer (210s), deathblow → life loss, timeout → vitality tiebreak, and round resets.

**Signal flow:** Listens for `DeathblowTriggered` from each PlayerBridge. Emits `LivesChanged`, `TimerChanged`, `MatchEnded` for the HUD.

**Round reset:** `FullReset()` on both players → economy reset, state reset, respawn at configured positions.

### StageController.cs — Stage Configuration

Configures floor size and wall positions based on `StageSize` enum (Small 10m, Medium 20m, Large 35m). Resizes visual floor mesh, physics floor collision, and positions 4 boundary walls.

### Archetypes (`src/bridge/archetypes/`)

- **IArchetypeVisuals**: `GetHitboxShape(AttackTier) → Shape3D`
- **LongswordVisuals / GreatswordVisuals**: Sealed singletons with pre-allocated `Shape3D` instances (avoid GC pressure during `IntersectShape` queries). Shapes match CLAUDE.md §8.

---

## 4. Presentation Layer (`src/presentation/`)

### HUD.cs — Tweened Health Bars

`CanvasLayer` subclass. Reads PlayerBridge accessors every `_Process` frame. Uses `Tween` objects for smooth bar transitions (Quad ease-out, 0.25s duration). Caches bar targets via `Dictionary<ProgressBar, float>` to avoid redundant tween creation when values haven't changed.

Stack labels update only when value changes (dirty check via `_lastP1Stacks` / `_lastP2Stacks`).

Timer display formatted as `M:SS`.

### DebugHUD.cs — Debug Overlay

`CanvasLayer` subclass. Toggled with **F3** (starts hidden). Displays per-player debug information:

- **State**: Current state machine state with frame countdown (e.g., "Coil T2 [14f]", "Dodge Active [8f]")
- **Attack Tier**: Current tier being used
- **Economy values**: Vitality, Composure, Momentum (numeric, not just bars)
- **Stacks**: Frame Advantage Stack count
- **Flags**: ActionLocked, Hitbox/Armor active, Fatigued, Terminal, Deathblow Vulnerable
- **Buffer**: Number of queued inputs
- **Last Event**: Most recent combat event
- **FPS**: Engine frames per second (bottom-center)

Uses `RichTextLabel` with BBCode for colored flag indicators (red YES / gray no). Panels have semi-transparent black background for readability. Only updates when visible to avoid unnecessary per-frame work.

---

## 5. Scene Coordination (`src/scene/`)

### GameCoordinator.cs — Player Wiring & Two-Pass Orchestration

Wires P1↔P2 opponent references, assigns HitboxManager ownership, registers P2 input actions programmatically (Arrow Keys + Numpad), and drives the two-pass physics update.

**Two-pass `_PhysicsProcess`:**
1. `Player1.TickPhase(delta)` + `Player2.TickPhase(delta)` — both state machines advance
2. `Player1.ResolvePhase()` + `Player2.ResolvePhase()` — hitbox queries run with symmetric state

This prevents tick-order asymmetry in clash detection. Each PlayerBridge has `SetCoordinatorDriven()` called in `_Ready` to disable its individual `_PhysicsProcess`.

**P2 input registration:** Uses `InputMap.AddAction()` + `InputEventKey` with `PhysicalKeycode` to create P2-suffixed actions at runtime. Checks `InputMap.HasAction()` to avoid re-registration.

### MainCoordinator.cs — Split-Screen Setup

Wires `RemoteTransform3D` → Camera nodes across the scene tree, and assigns `CameraController` targets (owner/opponent characters).

### CameraController.cs — Split-Screen Camera

`Node3D` subclass with `TopLevel = true` (detaches from parent transform hierarchy).

**Position:** Locks horizontal position to owner character. Height varies inversely with opponent distance (closer → higher camera), smoothly interpolated via `Mathf.Lerp`.

**Rotation:** Yaw tracks opponent direction. Pitch tracks vertical angle to opponent, clamped to `[MinPitch, MaxPitch]`. Both interpolated via `AngleDifference` × `RotationSpeed` × delta.

`SpringArm3D` child handles environment clipping prevention. Length configured via `[Export]`.

---

## 6. Data Flow Diagram

```
GameCoordinator._PhysicsProcess(delta)
      │
      ├── TICK PASS (both players advance before any resolution)
      │       │
      │       ├── Player1.TickPhase(delta)
      │       │       ├── BuildPlayerInput()    → world-space PlayerInput struct
      │       │       ├── _sim.Tick(input)       → state machine + economy
      │       │       │       ├── InputBuffer.Add/Consume/Tick
      │       │       │       ├── ComposureRecovery (cooldown + V² scaling)
      │       │       │       ├── StackDecay tick
      │       │       │       ├── RoW Momentum (60° cone check)
      │       │       │       └── ProcessState() → state transition
      │       │       ├── ApplyMovement()        → MoveAndSlide() + knockback decay
      │       │       ├── FaceOpponent()          → Yaw snap
      │       │       └── ApplyBoundaryPushback()
      │       │
      │       └── Player2.TickPhase(delta)  (same as above)
      │
      └── RESOLVE PASS (symmetric hit detection)
              │
              ├── Player1.ResolvePhase()
              │       ├── HitboxManager.ProcessHitboxes()
              │       │       ├── IntersectShape() → Hit/miss query
              │       │       └── ResolveHit()     → _sim.OnHit*/OnClash/etc.
              │       └── EmitCombatEvents()       → Godot signals → HUD
              │
              └── Player2.ResolvePhase()  (same as above)
```

---

## 7. Key Design Patterns

| Pattern | Where | Purpose |
|---------|-------|---------|
| Simulation-Observer | Global architecture | Godot-independent deterministic core |
| Sealed classes/records | All concrete types and state records | Prevent inheritance, enable devirtualization |
| Singleton (private ctor + static instance) | Archetype data/visuals | Stateless, zero-allocation |
| Immutable records | Player states | Safe `with` expressions, value equality |
| Sealed record | EconomyConstants | Immutable config record, constructor validation |
| TTL priority queue | InputBuffer | Frame-buffered input with priority consumption |
| Lazy initialization | PlayerBridge action names, HitboxManager query params/exclude RIDs | Avoid race conditions, defer allocation |
| Edge-detected + held input | PlayerInput struct | Separate press events from continuous state |
| Per-tick output flags | PlayerSimulation | Bridge reads outputs after Tick without coupling |
| Two-pass coordination | GameCoordinator | Tick all → Resolve all for symmetric frame processing |
| Signal-based event flow | Bridge → Presentation | Decoupled observer pattern via Godot signals |

---

## 8. Combat Resolution Cheat Sheet

```
Attacker in Swing phase → IntersectShape() hits defender
      │
      ├── Defender is Deathblow-vulnerable (Composure ≥ 1.0 or Vitality ≤ 0)?
      │       YES → DEATHBLOW EXECUTION (any tier, even blocked → life lost)
      │
      ├── Both swinging, same tier?
      │       YES → CLASH (mutual recovery, momentum surge)
      │
      ├── Defender has active armor (T3 Swing)?
      │       YES → ARMOR TRADE (1.5× vitality to defender, no stagger)
      │
      ├── Defender is Parrying?
      │       ├── Attacker in shatter window + can afford 3.0?
      │       │       YES → SHATTER (parry broken, full damage + stagger)
      │       └── NO → PARRY SUCCESS (no damage, +1 stack to defender)
      │
      ├── Attacker in shatter window + can afford 3.0?
      │       YES → SHATTER WHIFF (no damage, no momentum, extra recovery)
      │
      └── DEFAULT → STANDARD HIT/BLOCK
              ├── Blocked T1-T3 → chip vitality (20%), full composure, knockback
              ├── Blocked T0 → composure only (no vitality chip)
              └── Unblocked → full vitality + composure damage, stagger + knockback (20%)
```

---

## 9. Adding a New Archetype

1. Add a value to `ArchetypeType` enum in `src/simulation/ArchetypeType.cs`
2. Create `src/simulation/archetypes/NewArchetypeData.cs` — implement `IArchetypeData`, define `GetMoveData` with frame data and multipliers per tier
3. Create `src/bridge/archetypes/NewArchetypeVisuals.cs` — implement `IArchetypeVisuals`, define hitbox shapes per tier as pre-allocated static fields
4. Add a branch to `PlayerBridge.InitializeArchetype()` for the new enum value
5. Update `CLAUDE.md` §8 with the new archetype's frame data, multipliers, hitbox shapes, and matchup notes
6. Update this file (§2 Archetypes section and §3 Archetypes section)

---

## 10. Playtesting Workflow

All economy/frame constants are exposed as `[Export]` properties on `PlayerBridge`, organized by `[ExportGroup]`:

- **Economy group:** Momentum costs (dodge, jump, parry, shatter), generation rates, damage bases, armor multiplier, chip damage multiplier, stagger knockback multiplier, stack offset, stack decay timings, composure recovery cooldown
- **Frame Constants group:** Parry window, dodge startup/active/recovery, jump startup/active/recovery, evasion fatigue penalties, clash recovery, shatter whiff penalty, input buffer TTL
- **Setup group:** Archetype selection, speeds, camera pivot, player index

Adjust values in the Godot Inspector at runtime. Changes take effect on the next `_Ready()` call (scene reload). No recompilation needed for balance iteration.

---

## 11. Phase 2 Migration Notes

### LogicBlocks State Machine

The current `object _state` + `switch` pattern works but lacks:
- Compile-time exhaustive transitions (the `_ => new Idle()` fallback masks missing cases)
- Hierarchical states (e.g., "Attacking" parent state containing Coil/Swing/Recovery)
- Native serialization for rollback snapshots

Chickensoft LogicBlocks v5.20.0 is already imported as a NuGet dependency. Migration path:
1. Define state interfaces/classes using LogicBlocks API
2. Define transitions as strongly-typed input → state mappings
3. Replace `ProcessState` switch with LogicBlocks dispatch
4. Add `MemoryPack` serialization attributes for rollback

### Rollback Netcode

The simulation is already rollback-ready by design:
- Pure C#, no Godot dependencies
- Deterministic via Fixed64
- Single `Tick(PlayerInput)` entry point
- InputBuffer is simulation-owned

Remaining work:
1. Implement `MemoryPack` serialization on `PlayerSimulation` (snapshot to byte array)
2. Build ring buffer of N frames of state + input history
3. On input correction: restore snapshot → re-simulate forward
4. Implement `DeterministicRandom` from FixedMathSharp for visual variance

### Unified Entry Point Gap

**Current state:** `PlayerSimulation` exposes 8 public mutation methods (`OnHitReceived`, `OnHitLanded`, `OnClash`, `TryInitiateShatter`, `OnShatterLanded`, `OnShatterWhiff`, `OnParrySuccess`, `OnDeathblowTriggered`) that are called by `HitboxManager` *after* `Tick()` returns. This means a single frame's simulation state is mutated across two call sites: `Tick(input)` and the subsequent combat event callbacks.

**Why it matters for rollback:** Rollback requires replaying a frame from stored data alone. If only `PlayerInput` is stored per frame, the combat callbacks cannot be replayed — the hit/miss result from `IntersectShape()` (a Godot physics query) is lost. Snapshots taken at tick boundaries would not capture the post-callback mutations.

**Resolution strategy:** Replace the 8 public mutation methods with a single `ApplyCombatResult(CombatResult result)` method. `HitboxManager` builds a serializable `CombatResult` struct from the physics query, then the simulation processes it deterministically. The full frame contract becomes: `Tick(input)` → Bridge does physics → `ApplyCombatResult(result)`. For rollback, store both `PlayerInput` + `CombatResult` per frame.

**Why deferred:** This refactor touches `PlayerSimulation` (~8 methods → 1), `HitboxManager` (resolution logic must build structs instead of calling methods), and `PlayerBridge` (passthrough API). `TryInitiateShatter` is currently a query-with-side-effects (returns bool, spends Momentum) that `HitboxManager` uses to branch resolution — some resolution logic would need to move into the simulation. This overlaps heavily with the LogicBlocks migration and MemoryPack serialization work. All three should be coordinated in Phase 2.

### Tick-Order Asymmetry — RESOLVED

**Problem:** Godot calls `_PhysicsProcess` sequentially per node. In the original design, each PlayerBridge ran its full pipeline (tick → hitbox → resolve) independently, creating a frame-order race condition when both players attacked on the same frame.

**Solution (implemented):** `GameCoordinator` now drives a two-pass update:
1. **Tick pass:** `Player1.TickPhase(delta)` → `Player2.TickPhase(delta)` — both state machines advance
2. **Resolve pass:** `Player1.ResolvePhase()` → `Player2.ResolvePhase()` — hitbox queries run with symmetric state

Each PlayerBridge has `SetCoordinatorDriven()` called to disable its individual `_PhysicsProcess`. See §5 GameCoordinator and §6 Data Flow Diagram.

---

## 12. Testing

### Framework

GdUnit4 v4.4.1 via NuGet (`gdUnit4.api`). Tests live in `tests/` and cover the simulation layer (pure C#) and bridge integration. All test classes use `[TestSuite]` and `[TestCase]` attributes with `partial` classes. Bridge integration tests are annotated `[RequireGodotRuntime]` and use `ISceneRunner` to load `Game.tscn`.

### Test Suites

| File | Tests | Coverage Area |
|------|-------|---------------|
| `InputBufferTests.cs` | 12 | TTL queue, priority order, expiry, duplicates |
| `EconomyHandlerTests.cs` | 39 | Momentum, Vitality, Composure, stacks, fatigue, recovery, edge cases |
| `PlayerSimulationStateTests.cs` | 56 | State transitions, action lock, buffer preservation, RoW, penalty system |
| `AttackSystemTests.cs` | 56 | Coil→Swing→Recovery, frame data, armor, chip damage, stagger, interrupts |
| `EvasionSystemTests.cs` | 34 | Dodge/Jump 3-phase, fatigue degradation, direction restriction |
| `ShatterClashDeathblowTests.cs` | 46 | Shatter window/cost/whiff, Clash, Deathblow, penalty interactions |
| `CombatResolverTests.cs` | 35 | All 8 HitOutcome paths, resolution priority, knockback |
| `ArchetypeDataTests.cs` | 56 | Frame data, multipliers, cross-archetype invariants, constants |
| `BridgeIntegrationTests.cs` | 22 | Scene wiring, input flow, signals, RoundManager, fatigue edge detection |
| **Total** | **356** (334 headless + 22 Godot-runtime) | |

### Running Tests

- **`dotnet test`** runs 334 tests (all suites except `BridgeIntegrationTests`). The 22 `[RequireGodotRuntime]` tests are excluded when no Godot process is available, producing the "Failed to connect: Connection timeout" message — this is expected, not an error.
- **Godot editor** (GdUnit4 plugin) runs all 356 tests including bridge integration.

To run bridge tests from the command line, set `GODOT_BIN` to the Godot executable path before calling `dotnet test`.

### Simulation Layer Coverage (~95%)

All meaningful code paths in the simulation layer are covered. Remaining gaps are deliberately untested:

- **`_ => new Idle()` fallback** in `ProcessState` — defensive dead code, unreachable via valid inputs
- **`FatigueExited` signal** — momentum recovering from 0 back above 0 has no dedicated test (bridge integration only)
- **Export overrides** (e.g., `StackDecayDelayFrames = 0`) — editor-only configuration edge cases

### Impossible Game States — Audit Clean

All `OnHitReceived(wasBlocked: true)` calls are preceded by `sim.Tick(BlockHeldInput())` to place the sim in Blocking state first. This was systematically verified and enforced. Other handlers (`OnParrySuccess`, `OnClash`, `OnDeathblowTriggered`) are called in isolation to test Economy mutation in the handler itself — this is valid since Bridge calls them post-Tick regardless of state.

### Running Tests

Tests run inside the Godot editor via the GdUnit4 plugin (all 356), or via command line for the headless simulation suite:
```
dotnet test
```
This runs 334 tests. The 22 `[RequireGodotRuntime]` tests in `BridgeIntegrationTests.cs` are excluded when no Godot process is reachable — "Failed to connect: Connection timeout" is expected output, not an error. To include bridge tests from the CLI, set the `GODOT_BIN` environment variable to your Godot executable path before running `dotnet test`.

### TestHelpers

`tests/TestHelpers.cs` provides factory methods:
- **Input factories**: `EmptyInput()`, `AttackInput(tier)`, `BlockParryPressInput()`, `BlockHeldInput()`, `DodgeInput()`, `JumpInput()`, `MoveForwardInput()`, `MoveSidewaysInput()`, `AirborneInput()`, `DodgeForwardInput()`, `BlockWalkForwardInput()`, `BlockWalkSidewaysInput()`, `RunForwardInput()`, `AttackWhileMovingInput(tier)`
- **Sim helpers**: `CreateSim(archetype)`, `TickN(sim, n)`, `AdvanceToSwing(sim, tier)`, `AdvanceToRecovery(sim, tier)`, `CompleteFullAttack(sim, tier)`

---

## 13. Feature Verification Checklist

Split into two lists. **Automated** items have passing `[TestCase]` entries in `tests/`. **Manual** items require running the project — they involve physics integration, visual behavior, or subjective feel that cannot be reliably captured in unit tests without excessive fragility.

---

### Automated Verification

Check off when the corresponding test passes (run via GdUnit4).

#### Input Buffer
- [ ] Buffer TTL = 6 frames (inputs persist for ~100ms before expiry)
- [ ] Priority order: BlockParry > Attack > Dodge > Jump
- [ ] Duplicate inputs rejected (same action not buffered twice)
- [ ] Expired entries removed after TTL reaches 0
- [ ] Consume returns `None` on empty buffer

#### Attack System
- [ ] T0/T1/T2/T3 Coil frames match archetype data (Longsword: 4/8/16/24, Greatsword: 8/14/24/36)
- [ ] Swing frames match archetype data
- [ ] Recovery frames match archetype data
- [ ] Coil → Swing → Recovery → Idle cycle completes correctly
- [ ] Frame Advantage Stacks reduce Coil frames (stacks × `FrameAdvantageOffset`)
- [ ] All stacks consumed when attack fires
- [ ] Coil cannot be reduced below 1 frame
- [ ] Attack input works from both Idle and Moving states
- [ ] HitboxActive = true only during Swing phase
- [ ] Hit during Coil staggers the attacker (attack interrupted)
- [ ] Hit during Swing staggers the attacker (non-armored T1/T2)
- [ ] Hit during Recovery staggers the player
- [ ] `CurrentTier` set immediately when attack is entered; preserved through Recovery
- [ ] `ResetState` clears pending shatter whiff recovery (no corrupt extra frames next attack)

#### Standard Block
- [ ] No Momentum cost; always available including during Fatigue
- [ ] Held block keeps player in Blocking state; release returns to Idle
- [ ] Blocked T1-T3: chip vitality damage (20% of full), full composure damage
- [ ] Blocked T0: composure damage only, zero vitality chip
- [ ] Does NOT reset Frame Advantage Stacks
- [ ] Cannot prevent Deathblow — any hit while vulnerable triggers execution even if blocked

#### Perfect Parry
- [ ] Costs 0.5 Momentum; reverts to Standard Block if insufficient (Fatigue)
- [ ] Grants +1 Frame Advantage Stack on success
- [ ] Active parry window = `EffectiveParryWindowFrames` (base 6, halved per penalty)
- [ ] Transitions to Blocking when window expires

#### Premature Press Penalty
- [ ] Whiffed parry (window expired without success) increments penalty
- [ ] Shatter whiff increments penalty
- [ ] Each penalty halves effective window (ceil division: 6 → 3 → 2 → 1, min 1)
- [ ] Penalty shared between parry and shatter
- [ ] Successful parry resets penalty
- [ ] Successful shatter resets penalty
- [ ] `ResetState` clears penalty
- [ ] After 30 frames of no `block_parry` press, penalty decays to 0
- [ ] Inactivity timer resets on new `block_parry` press

#### Evasion — Dodge
- [ ] Only lateral or backward dodges accepted (no forward dodge)
- [ ] Dodge with no stick input rejected
- [ ] Costs 1.5 Momentum when affordable; degraded when unaffordable (no momentum deducted)
- [ ] Normal frames: 3 startup / 12 active / 3 recovery = 18 total
- [ ] Degraded frames: 7 startup / 8 active / 3 recovery = 18 total
- [ ] Action-locked through all three phases
- [ ] Consecutive dodge possible immediately after returning to Idle

#### Evasion — Jump
- [ ] No direction required
- [ ] Costs 1.0 Momentum when affordable; degraded when unaffordable (no momentum deducted)
- [ ] Normal frames: 3 startup / 22 active / 5 recovery = 30 total
- [ ] Degraded frames: 7 startup / 18 active / 5 recovery = 30 total
- [ ] Ground contact during active phase triggers early transition to recovery
- [ ] Action-locked through all three phases
- [ ] Consecutive jump possible immediately after returning to Idle

#### Shatter Technique
- [ ] Shatter window active for `EffectiveParryWindowFrames` frames after last `block_parry` press
- [ ] Initial shatter window is false before any `block_parry` press
- [ ] Costs 3.0 Momentum (paid regardless of outcome)
- [ ] Shatter against Parry: full damage + stagger (ShatterLanded)
- [ ] Shatter against Block: whiff — no damage, extra recovery frames
- [ ] Shatter against unblocked: whiff — no damage, extra recovery frames
- [ ] Shatter that cannot be afforded degrades to normal hit/block resolution
- [ ] No momentum reward on shatter whiff

#### Clash System
- [ ] Same tier + both hitboxes active = Clash (neither gets stagger)
- [ ] Different tiers do NOT clash
- [ ] Both players receive `ClashMomentumSurge` (2.0)
- [ ] Both players enter Recovery (`ClashRecoveryFrames` = 8)
- [ ] `ClashedThisFrame` guard prevents double-processing
- [ ] `OnClash` forces Recovery from any state (Idle, Blocking, Swing, etc.)

#### Stagger Hierarchy
- [ ] T0 unblocked: minor vitality damage only (no composure, no stagger)
- [ ] T0 blocked: composure damage only (no vitality chip)
- [ ] T3 armor active during Swing phase only (not Coil)
- [ ] T3 armor disabled when Fatigued
- [ ] T3 armor trade: 1.5× vitality to defender, no stagger
- [ ] Stagger duration matches per-archetype per-tier `StaggerFrames` from MoveData
- [ ] Stagger returns to Idle when frames expire

#### Momentum Economy
- [ ] Starts at 4.0 (half of max 8.0)
- [ ] Walking toward opponent (+0.05/frame, 60° cone) generates momentum
- [ ] Running toward opponent (+0.1/frame) generates more than walking
- [ ] RoW does not generate while action-locked or moving perpendicular/away
- [ ] `IsMovingToward` returns false when opponent is at the same position
- [ ] `BaseMomentumOnHit` (0.5) added on every landed or blocked hit
- [ ] ClashSurge (2.0) applied on clash
- [ ] IsFatigued when Momentum ≤ 0: disables Parry, T3 armor; degrades evasion frames

#### Composure Economy
- [ ] Recovery paused for 90 frames after any composure damage
- [ ] Recovery cooldown resets on each new composure damage instance
- [ ] Recovery rate = `ComposureBaseRecoveryRate` × Vitality² (quadratic scaling)
- [ ] Terminal vitality (< 0.1) halts recovery entirely
- [ ] Composure damage still applies even at Terminal vitality

#### Vitality Economy
- [ ] Damage = `BaseVitalityDamage` (0.08) × move vitality multiplier
- [ ] Chip damage = 20% of full damage on blocked T1-T3
- [ ] Vitality at 0 makes player DeathblowVulnerable

#### Frame Advantage Stacks
- [ ] +1 stack per `OnParrySuccess`
- [ ] All stacks consumed when coil reduction fires
- [ ] `ConsumeFrameAdvantageCoilReduction` returns 0 when stacks = 0
- [ ] Reset on any hit received (blocked or unblocked)
- [ ] Not reset by entering Blocking state
- [ ] Decay begins after 180 frames (3s) of no parry; -1 every 60 frames (1s)
- [ ] Decay timer resets after `ConsumeFrameAdvantageCoilReduction`
- [ ] Stack count never goes below 0

#### Deathblow System
- [ ] Triggers on any hit (blocked or unblocked) once `IsDeathblowVulnerable`
- [ ] `IsDeathblowVulnerable` when Composure ≥ 1.0 OR Vitality ≤ 0
- [ ] Deathblow state is action-locked, movement halted, persists across ticks
- [ ] `ResetState` clears Deathblow and returns to Idle

#### Action Commitment & Buffer
- [ ] All inputs ignored during Coil, Swing, Recovery, Parrying, Dodging, Jumping, Staggered, Deathblow
- [ ] Buffer preserved (not consumed) during action-locked states
- [ ] Buffer NOT consumed while in Blocking state
- [ ] Attack pressed during Blocking is buffered and fires after block released
- [ ] `block_parry` pressed late in Recovery is buffered and fires as Parrying after Idle

#### Right-of-Way (RoW)
- [ ] RoW generated in Idle, Moving, and Blocking states only
- [ ] No RoW during action-locked states
- [ ] Perpendicular movement does not generate RoW
- [ ] Moving away does not generate RoW

#### Input Modifier Tier Selection
- [ ] No modifier held → Standard (T1)
- [ ] `modifier_light` → T0; `modifier_heavy` → T2; `modifier_super` → T3
- [ ] Multiple modifiers: Super > Heavy > Light (last `if` wins)

#### Bridge Integration (`[RequireGodotRuntime]`)
- [ ] Both players start in Idle after scene load
- [ ] Default archetypes assigned: P1 = Longsword, P2 = Greatsword
- [ ] Initial economy values: Momentum 4.0, Vitality 1.0, Composure 0.0
- [ ] Export defaults match spec values (momentum costs, frame constants, speeds)
- [ ] Block-walk speed is lower than normal walk speed
- [ ] Run is suppressed while blocking
- [ ] Slide-to-stop: non-zero velocity on first frame of committed action from movement
- [ ] Both players wired by GameCoordinator (HitboxManagers assigned, opponents cross-wired)
- [ ] Attack input flows through bridge to Coil state
- [ ] Block input flows through bridge to Parrying/Blocking state
- [ ] `HitLanded`, `ParrySuccess`, `ShatterWhiff`, `ClashEvent` signals emitted correctly
- [ ] `FullReset` restores all economy values; `ResetState` returns to Idle
- [ ] `PrematureBlockPenalties` and `EffectiveParryWindow` accessible via bridge
- [ ] `FatigueEntered` signal fires exactly when Momentum reaches 0 (edge-detected)
- [ ] `RoundManager`: P1 Deathblow decrements P2 lives; `LivesChanged` signal fires

---

### Manual Verification (Playtesting)

Verify by running the project. These behaviors involve physics integration, visual output, or subjective feel beyond the reach of unit tests.

#### Movement Feel
- [x] Walk speed (4.0) and run speed (7.0) feel appropriately paced for the stage sizes
- [x] Auto-facing opponent: character rotates smoothly to track, no snap artifacts
- [x] Slide-to-stop looks natural over ~15 frames; not too stiff, not too floaty
- [x] Deathblow/Staggered halt is immediate and readable

#### Knockback Physics
- [x] Block knockback: defender is pushed back a noticeable distance on T1-T3 hits
- [x] Stagger knockback (20% of block): shorter push, clearly less than blocking
- [x] Clash knockback: both players pushed apart symmetrically
- [ ] Knockback velocity decays smoothly over multiple frames (0.75× per frame)
- [x] While knockback is active, player cannot walk through it (movement suppressed)
- [x] T0 attacks produce no knockback on block
- [x] Knockback from T2 (weapon recoil) feels more disruptive than T1

#### Block-Walk Feel
- [x] Block-walk speed (2.0) visibly slower than normal walk (4.0); distinctly slower than run (7.0)
- [x] Block-walk movement is fluid; no stuttering when holding block + stick input

#### Input System (Two-Player)
- [x] P2 controls (Arrow Keys + Numpad) fully functional independent of P1
- [x] Both players can act simultaneously with no cross-interference

#### Boundary Pushback
- [x] Invisible walls push players back at `BoundaryPushbackStrength` (5.0)
- [x] Pushback is not exploitable for locking an opponent against a wall (force feels appropriate)

#### HUD & Presentation
- [x] Vitality bar depletes smoothly; tween (0.25s, Quad out) tracks damage without snapping
- [x] Composure bar fills smoothly; tween visible during pressure sequences
- [x] Composure recovery (0.0036/frame × V²) is visually perceptible during lulls; faster at full health, negligible near zero
- [x] Momentum gauge (8 segments) updates correctly without flickering
- [x] Stack counter (3D or HUD) increments on parry, resets on hit; decay is visible over time
- [x] Timer counts down in `M:SS` format; shows `0:00` on expiry
- [x] Lives display initializes correctly; decrements only on Deathblow or timeout

#### Combat Event Feedback
- [ ] `HitLanded` / `HitBlocked` signals wire to visible HUD effects (color flash, screen shake, etc.)
- [ ] `ParrySuccess` gives distinct visual/audio feedback vs `ShatterLanded`
- [ ] `ShatterWhiff` punishment (extra recovery) is readable from animation
- [ ] `ClashEvent` mutual knockback is symmetric and visually satisfying
- [ ] `FatigueEntered` / `FatigueExited` signals are visually indicated to the owning player
- [ ] `DeathblowTriggered` execution is clearly communicated

#### Camera & Split-Screen
- [ ] Both viewports occupy 50% of screen with no overlap or gap
- [x] Each camera tracks behind/above its owning player
- [x] Yaw + pitch interpolation toward opponent feels responsive without snapping
- [x] `SpringArm3D` prevents camera clipping through stage geometry
- [x] Camera height increases slightly as opponent approaches (closer = higher angle)
- [x] `TopLevel = true` — camera does not inherit player's rotation

#### Momentum Visibility (§4.1 Design Intent)
- [ ] Momentum gauge is visible only in the owning player's viewport; opponent's viewport shows no gauge (or a blank/hidden gauge per `momentum_ui_visibility` setting)

#### Debug Overlay
- [x] F3 toggles debug HUD; starts hidden at launch
- [x] All fields update correctly: state name, tier, Vitality/Composure/Momentum values, flags, buffer count, last event
- [x] Penalty and effective window fields visible and correct
- [x] FPS counter accurate and readable

#### Stage System
- [x] Small (10m), Medium (20m), Large (35m) stages load without error
- [x] Floor mesh and collision correctly sized; no gaps or misaligned walls
- [x] Invisible boundary walls stop movement; pushback prevents wall-sticking

#### Round Structure Feel
- [x] Timeout fires at 210 seconds; vitality tiebreak winner resolved correctly
- [x] Exact vitality tie: no life lost, new round starts (verify with equal damage taken)
- [x] 2-second delay between rounds feels appropriately paced
- [x] Full reset between rounds: positions, all economy values, states restored
- [ ] Match ends correctly when one player reaches 0 lives; winner displayed

#### Two-Pass Architecture (Fallback)
- [ ] Standalone `PlayerBridge._PhysicsProcess` (without `GameCoordinator`) still runs correctly for solo testing scenes
