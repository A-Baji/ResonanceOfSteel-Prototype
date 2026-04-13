# Resonance of Steel — Prototype Source of Truth

> **AI AGENT INSTRUCTION — SOURCE OF TRUTH**
> Any AI agent working on this repository **MUST** read this file at session start. Update it before session end if architectural changes, new systems, or design shifts occurred. This document defines what the project **is** and what it **should be**. Do not contradict it without explicit developer approval.
>
> **DEVELOPER GUIDE MAINTENANCE:** When making code changes that affect architecture, combat resolution logic, data flow, file purposes, or adding new systems, also update `docs/dev_guide.md` to reflect those changes. The dev guide is the developer-facing documentation companion to this file.

---

## 1. Project Identity

**Resonance of Steel** is a local split-screen 1v1 competitive melee combat prototype built in **Godot 4 (.NET / C#)**. It implements the "Spatial Rhythm Framework" — a combat system built on a hidden Momentum economy, a tiered stagger hierarchy (Tiers 0–3), and a defensive duality separating rhythmic mastery (Parrying) from spatial management (Evasion).

The prototype exists to validate economy values, stagger interactions, and defensive duality through playtesting. It is **not** a shippable product. Placeholder assets (capsule rigs, primitive geometry) are expected and intentional.

**Two playable archetypes**: Longsword (pure fundamentals, consistent tempo) and Greatsword (commitment and consequence, volatile resource curve).

---

## 2. Architecture: Simulation-Observer Pattern

The codebase enforces a strict three-layer architecture. Dependencies flow inward only. **No layer may violate its boundary.**

```
┌─────────────────────────────────────────────┐
│         Presentation Layer (Observer)        │
│   HUD.cs, AnimationPlayer, Particles, Audio │
│   Reads Bridge state. Never writes.         │
│   Interpolates visuals between 60fps ticks. │
├─────────────────────────────────────────────┤
│            Bridge Layer (Conduit)            │
│   PlayerBridge.cs, HitboxManager.cs,        │
│   RoundManager.cs, StageController.cs,      │
│   Archetype Visuals (Godot shapes)          │
│   Polls input → calls Tick() → applies      │
│   physics results → emits events.           │
│   Contains ZERO combat logic.               │
├─────────────────────────────────────────────┤
│        Simulation Layer (Pure C#)           │
│   PlayerSimulation.cs, EconomyHandler.cs,   │
│   PlayerStates.cs, InputBuffer.cs,          │
│   PlayerInput.cs, CombatEvent.cs,           │
│   EconomyConstants.cs, Archetype Data       │
│   NO Godot imports. Fixed64 math only.      │
│   Deterministic. Serializable.              │
└─────────────────────────────────────────────┘
```

### 2.1 Why This Architecture

The Simulation-Observer split is a **hard requirement** for Phase 2 rollback netcode. The simulation layer must be:
- **Godot-independent**: No `Node`, `Resource`, or engine types. Pure C# only.
- **Deterministic**: All combat math uses `Fixed64` via FixedMathSharp. No `float`/`double` in simulation.
- **Serializable**: Full state must be capturable as a byte array for snapshot/restore.
- **Input-driven**: Single `Tick(PlayerInput input)` entry point per physics frame.

### 2.2 Layer Rules

| Rule | Enforcement |
|------|-------------|
| Simulation never imports Godot namespaces | Compile-time — `src/simulation/` files must not `using Godot` |
| Bridge calls `Tick()` exactly once per `_PhysicsProcess` | PlayerBridge._PhysicsProcess() |
| InputBuffer owned by Simulation layer | PlayerSimulation enqueues from raw PlayerInput, consumes internally |
| Bridge queries simulation via semantic properties | `IsBlocking`, `IsParrying`, `IsInDeathblow` — no string-based state checks |
| Presentation reads but never writes simulation state | HUD accesses PlayerBridge public getters only |
| Hit detection uses manual `IntersectShape()` per tick | HitboxManager bypasses Area3D signal delay |
| Combat events flow Bridge → Presentation via signals | Bridge emits, Presentation observes |

---

## 3. Technology Stack

| Component | Solution | Status | Notes |
|-----------|----------|--------|-------|
| Language | C# (.NET 8.0) | ✅ Active | Required for AOT/serialization compatibility |
| Engine | Godot 4 (.NET) | ✅ Active | Stable release |
| Determinism | [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp) v2.1.1 | ✅ Active | `Fixed64`, `Vector3d`, `FixedQuaternion`. MIT license. Actively maintained (weekly commits). .NET 8 compatible. Includes `DeterministicRandom`, `MemoryPack` serialization, bounding shapes. **All combat math must use this — no float/double in simulation.** |
| State Machine | [Chickensoft LogicBlocks](https://github.com/chickensoft-games/LogicBlocks) v5.20.0 | 📦 Imported, Phase 2 | Hierarchical statechart package. Serializable, AOT-safe, rollback-ready. Currently the hand-rolled record-based state machine is in use (see §5). **Phase 2 migration target** for serialization snapshots, diagram generation, and compound states as complexity grows. |
| Hitbox Detection | `PhysicsDirectSpaceState3D.IntersectShape()` | ✅ Active | Manual queries per physics tick. Bypasses 1-frame Area3D signal delay. |
| Input Buffer | Hand-rolled TTL Queue | ✅ Active | 6-frame TTL at 60fps. Priority: block > attack > dodge > jump > run. |
| Camera | Custom `CameraController.cs` | ✅ Active | Split-screen with opponent auto-focus. |
| Testing | [GdUnit4](https://github.com/MikeSchulze/gdUnit4Net) v4.4.1 | ✅ Active | C# test framework via NuGet `gdUnit4.api`. 194 tests across 7 test suites covering simulation layer. |

### 3.1 Rollback Netcode Strategy (Phase 2)

The design docs reference **Snopek Godot Rollback Netcode** (GDScript addon) and **Fractural GodotRollbackNetcodeMono** (C# wrapper). After research, **neither is recommended** for this project:

- **Snopek addon**: Core is 100% GDScript. Designed around `_save_state()`/`_load_state()` virtual methods on Godot Nodes in the `network_sync` group. Fundamentally assumes combat logic lives inside Nodes — the opposite of our pure-C# simulation architecture.
- **Fractural Mono wrapper**: 70% GDScript / 30% C# bridge code. 23 stars, 1 contributor, no releases. Requires `GetNodeAsWrapper<>()` calls to interop with GDScript singletons. The pure-C# port (`FracturalRollbackNetcode`) is **archived and deprecated** as of Feb 2025.

**Recommended approach**: Write a lightweight custom rollback manager in pure C# that:
1. Snapshots `PlayerSimulation` state to byte arrays (leveraging FixedMathSharp's `MemoryPack` serialization)
2. Stores a ring buffer of N frames of state + input history
3. On input correction: restore snapshot → re-simulate forward with corrected inputs
4. Runs entirely within the Simulation layer — no Godot dependency

This aligns naturally with the existing `Tick(input)` architecture. The simulation is already deterministic and Godot-independent; rollback is purely a matter of state save/restore + re-tick.

### 3.2 Removed/Rejected Dependencies

| Tool | Reason for Rejection |
|------|---------------------|
| Phantom Camera plugin | GDScript-based (83%). Project already has a working custom `CameraController.cs`. Adds unnecessary cross-language coupling for no benefit in a prototype. |
| Snopek Rollback Netcode | GDScript-only. Incompatible with pure-C# simulation architecture. See §3.1. |
| Fractural Rollback Mono | Thin GDScript wrapper, single maintainer, no releases. Pure C# port archived. See §3.1. |
| Sakuga Engine | Reference only — do not adopt as framework. Structural patterns may inform design but no code should be imported. |

---

## 4. Economy System

Three resources govern combat. All values are `Fixed64`.

### 4.1 Momentum (Hidden Resource)

| Property | Value |
|----------|-------|
| Range | 0.0 – 8.0 (8 segments) || Initial value | 4.0 (50% of max) || Hidden from opponent | Yes (visible only in owning player's viewport) |
| UI toggle | `momentum_ui_visibility` (deferred) |

**Costs:**

| Action | Cost |
|--------|------|
| Perfect Parry | 0.5 segments |
| Dodge (any lateral/back direction) | 1.5 segments |
| Jump | 1.0 segments |
| Shatter Modifier | 3.0 segments |

**Generation:**

| Source | Rate |
|--------|------|
| Landing any strike (hit or blocked) | `base_momentum_on_hit` = 0.5 |
| Walking toward opponent | `walk_momentum_rate` = 0.05/frame |
| Running toward opponent | `run_momentum_rate` = 0.1/frame |
| Clash (same-tier simultaneous hit) | `clash_momentum_surge` = 2.0 |

**Fatigue Cascade** (Momentum = 0.0):
- Lose ability to Perfect Parry (gated by `CanAfford(PerfectParryCost)`)
- Lose Brace / Super Armor (Tier 3 Swing armor disabled)
- Dodge and Jump still available but degraded: `EvasionFatigueStartupPenalty` (4) extra startup frames, `EvasionFatigueActiveReduction` (4) fewer active frames
- Only Standard Block remains fully effective
- Exit: Momentum must increase above 0.0 via Clash or strike landing
- **Note**: Fatigue is implemented as a flag (`EconomyHandler.IsFatigued`), not a distinct state. It modifies behavior of other states rather than being a state itself.

### 4.2 Composure (Deathblow Gauge)

| Property | Value |
|----------|-------|
| Range | 0.0 – 1.0 (normalized) |
| Deathblow trigger | Vitality ≤ 0 OR Composure ≥ 1.0, then any strike lands (any tier) |
| Recovery cooldown | `ComposureRecoveryCooldownFrames` = 90 (1.5s at 60fps) after taking composure damage |
| Recovery formula | $R_{comp} = B_{rate} \times (V_{curr} / V_{max})^2$ |
| Base recovery rate | `B_rate` = 0.0036/frame (only ticks after cooldown expires) |
| Terminal state | $V_{curr} < 0.1 \times V_{max}$ → recovery forced to 0 |

**Sekiro-inspired recovery design**: Recovery is paused for 90 frames (1.5s) after any composure damage. When active, recovery scales with the **square** of current Vitality — at 50% HP, recovery is 25% of max rate; at 25% HP, it's 6.25%. This creates a clear two-phase dynamic: early game deals Vitality damage to degrade Composure recovery, late game breaks Composure for the Deathblow.

### 4.3 Vitality (Lethality Regulator)

| Property | Value |
|----------|-------|
| Range | 0.0 – 1.0 (normalized) |
| Terminal threshold | `terminal_vitality_threshold` = 0.1 |
| Role | Not a direct win condition. Regulates Composure recovery. Reaching 0.0 makes Deathblow inevitable. |
| Damage formula | $base\_vitality\_damage \times move\_vitality\_multiplier$ |
| Base value | `base_vitality_damage` = 0.08 |

### 4.4 Frame Advantage Stacks

| Property | Value |
|----------|-------|
| Type | Integer, no upper limit |
| Per Perfect Parry | +1 stack |
| Coil reduction | `stacks × frame_advantage_offset` (1 frame per stack) subtracted from next attack's Coil |
| Reset conditions | Non-parried hit received (blocked or unblocked), or consuming frame advantage on counter-attack |
| Decay | After `StackDecayDelayFrames` (180 = 3s) of no parry activity, stacks decay by 1 every `StackDecayIntervalFrames` (60 = 1s) |

---

## 5. Player State Machine

The current state machine uses a `private object _state` pattern with C# record types. Each `Tick()` call checks input, transitions state, and decrements frame counters.

**Phase 2 target**: Migrate to Chickensoft LogicBlocks for hierarchical state support, serialization snapshots, and auto-generated state diagrams.

### 5.1 State Definitions

| State | Entry Condition | Exit Condition | Accepts Input |
|-------|----------------|----------------|---------------|
| **Idle** | Default grounded state | Any valid buffered input | All |
| **Moving** | Stick input, no run modifier | Stick released or run pressed | All |
| **Running** | Run held + forward stick | Run or stick released | All |
| **Coil** | Attack dequeued from buffer | Coil frames elapsed or interrupted | None (action-locked) |
| **Swing** | Coil phase complete | Swing frames elapsed | None (action-locked) |
| **Recovery** | Swing phase complete | Recovery frames elapsed | None (action-locked) |
| **Blocking** | `block_parry` held outside parry window | Input released | None |
| **Parrying** | `block_parry` pressed within parry window | Parry window (6 frames) elapsed | None (action-locked) |
| **Staggered** | Hit received or parry whiff | Stagger frames elapsed (per-archetype via `GetStaggerFrames(tier)`) | None (action-locked, movement halted) |
| **Dodging** | Dodge dequeued + directional input (not forward) | Startup → Active → Recovery phases elapsed | None (action-locked) |
| **Jumping** | Jump dequeued from buffer | Startup → Active (ground contact) → Recovery phases elapsed | None (action-locked) |
| **Fatigued** | *(Removed — flag-based)* | Momentum check gates Parry/Dodge/Armor | N/A |
| **Deathblow** | Composure ≥ 1.0 (or Vitality ≤ 0) and any strike lands | Execution animation complete | None (action-locked, movement halted) |

### 5.2 State Record Types

```csharp
record Idle;
record Moving(bool IsRunning);
record Coil(AttackTier Tier, int FramesLeft);
record Swing(AttackTier Tier, int FramesLeft);
record Recovery(int FramesLeft, AttackTier Tier);
record Blocking;
record Parrying(int FramesLeft);
record Dodging(int StartupLeft, int ActiveLeft, int RecoveryLeft);
record Jumping(int StartupLeft, int ActiveLeft, int RecoveryLeft);
record Staggered(int FramesLeft);
record Deathblow;
```

### 5.3 Action Commitment

All states except Idle, Moving, and Blocking are **action-locked**: the player is fully committed and cannot cancel into other actions. This enforces the "commitment and consequence" design philosophy.

**Input blocking:** The input buffer only consumes entries when the player is in Idle or Moving. During action-locked states, buffered inputs remain in the queue and expire via TTL. This means early inputs pressed near the end of an animation are preserved and consumed when the player returns to an actionable state.

**Slide-to-stop:** When entering a committed action from movement, the player's velocity decays at 0.85× per frame (friction-based slide) instead of stopping instantly. This produces a natural deceleration over ~15 frames. Stagger and Deathblow states halt movement completely (no slide).

---

## 6. Stagger Hierarchy

Every attack is classified by Stagger Tier (0–3).

| Tier | Name | Interrupt Capability | Properties |
|------|------|---------------------|------------|
| 0 | Flinchless | None — opponent continues their animation | Generates Momentum. Chip damage. Does NOT interrupt. See §6.1 |
| 1 | Standard | Interrupts T1 & T2 (Wind-up/Swing); T3 (Wind-up only) | Base rhythmic strike |
| 2 | Heavy | Interrupts T1 & T2 (Wind-up/Swing); T3 (Wind-up only) | Slower. Causes Weapon Recoil on block (resets neutral) |
| 3 | Committed | Interrupts all unarmored frames | Active Frame Armor during Swing phase |

### 6.1 Tier 0 Damage Conditions

Tier 0 damage is **conditional** on block state:
- **Unblocked** (direct hit): Minor Vitality damage only
- **Blocked** (Standard Block): Minor Composure buildup on blocker instead
- **Never both simultaneously**

### 6.2 Active Frame Armor (Tier 3 Only)

- **Coil phase**: Fully vulnerable — can be interrupted by any T1/T2 strike
- **Swing phase**: Ignores T1/T2 stagger but suffers **1.5× Lethality damage** from hits during trade
- **Disabled during Fatigue state**

---

## 7. Defensive System

### 7.1 Standard Block

- **No Momentum cost**, no timing requirement
- Universal fallback — **always available**, including during Fatigue
- Causes Composure buildup on the blocker
- **Chip damage**: Blocked T1–T3 attacks deal 20% Vitality damage (`ChipDamageMultiplier` = 0.2). T0 blocked attacks deal Composure only, no Vitality chip.
- **Knockback**: Blocked T1+ attacks push the blocker backward based on the move's `KnockbackDistance`. Knockback suppresses input movement until velocity decays.
- Attacker suffers no Composure penalty
- Blocking T2 (Heavy) causes Weapon Recoil (resets neutral spacing)
- **No effect on Shatter-modified strikes** — block occurs normally, Shatter whiffs
- **Does NOT reset Frame Advantage Stacks**
- **Cannot prevent Deathblow** — if opponent is Deathblow-vulnerable, any hit (even blocked) triggers execution

### 7.2 Perfect Parry

- Costs 0.5 Momentum segments
- Grants +1 Frame Advantage Stack
- **Unavailable during Fatigue**
- 6-frame parry window

### 7.3 Evasion

**Dodge:**
- Costs 1.5 Momentum segments (deducted on use)
- **Always available** regardless of Momentum — insufficient Momentum degrades the evasion (more startup, fewer active frames) rather than blocking it
- **Direction restriction**: Only lateral left, lateral right, or backward. Forward dashes are NOT permitted. Enforced via dot-product check against opponent direction.
- Three-phase structure: Startup (vulnerable) → Active (i-frames) → Recovery (vulnerable)
- Default frames: 3 startup / 12 active / 3 recovery (18 total)
- Fatigue penalty: +4 startup, −4 active (7 startup / 8 active / 3 recovery = 18 total)

**Jump:**
- Costs 1.0 Momentum segments (deducted on use)
- **Always available** regardless of Momentum — insufficient Momentum degrades the evasion
- Three-phase structure: Startup (grounded) → Active (airborne, ground contact triggers early landing) → Recovery (landing)
- Default frames: 3 startup / 22 active / 5 recovery (30 total)
- Fatigue penalty: +4 startup, −4 active (7 startup / 18 active / 5 recovery = 30 total)

**Evasion grants** "Clean Whiff" but no stacks. Reduced Composure damage on post-evasion punishes.

### 7.4 The Shatter Technique

A timing-based input modifier applied to any attack tier. High-risk "Parry-Trap."

| Outcome | Effect |
|---------|--------|
| Defender Perfect Parries a Shatter strike | Parry **Shatters**: full damage + massive Composure strain |
| Defender Standard Blocks a Shatter strike | Shatter **whiffs**: 3.0 Momentum cost + `ShatterWhiffPenaltyFrames` extra recovery. **No damage dealt.** |
| Defender is not blocking | Shatter **whiffs**: 3.0 Momentum cost + extra recovery. **No damage dealt** — the Shatter commitment nullifies the hit. |
| Shatter on Tier 0 | Permitted but inadvisable — damage insufficient to justify Momentum cost |

**Shatter detection**: A Shatter activates when the attacker inputs `block_parry` within the parry window during their Swing phase at the moment of hitbox contact, AND affords the 3.0 Momentum cost. The cost is paid on activation regardless of outcome. The only **successful** Shatter is when the defender was Perfect Parrying — all other outcomes are whiffs where the Shatter commitment nullifies the hit entirely (no damage, no momentum reward). The only additional punishment for whiffing is the extra recovery frames.

### 7.5 The 5-Frame Clash Window

When two attacks of the same tier collide within 5 frames of each other:
- Both players receive `clash_momentum_surge` (2.0)
- Both players receive knockback based on the clashing move's `KnockbackDistance`
- Cooldown period follows to prevent mash-spam
- **Initiating any attack resets Frame Advantage Stacks** (per Stack Reset Rules), so a Clash also resets the attacking player's stacks
- Clash is a **Momentum recovery tool only** — no stack interaction

---

## 8. Archetype Data

### 8.1 Longsword — "Pure Fundamentals"

The baseline archetype. No gimmicks. Every advantage is manufactured through framework mastery.

| Attribute | Value |
|-----------|-------|
| Reach | Mid-Range |
| Speed | Standard (baseline) |
| Stagger Access | Universal (all 4 tiers viable) |
| Momentum Curve | Stable — rarely approaches Fatigue |

#### Frame Data (60fps)

| Move | Tier | Coil | Swing | Recovery | Total |
|------|------|------|-------|----------|-------|
| Flick | 0 | 4 | 2 | 4 | 10 |
| Cross Cut | 1 | 8 | 4 | 6 | 18 |
| Overhead | 2 | 16 | 6 | 10 | 32 |
| Lunge | 3 | 24 | 8 | 13 | 45 |

#### Damage Multipliers

| Move | Vitality | Composure | Notes |
|------|----------|-----------|-------|
| Flick | 0.2× | 0.1× (blocked only) | T0 conditional damage |
| Cross Cut | 1.0× | 1.0× | Baseline multiplier |
| Overhead | 1.5× | 1.75× | Primary Composure pressure |
| Lunge | 2.0× | 2.0× | 1.5× incoming Lethality during armor trade |

#### Hitbox Shapes

| Move | Shape | Dimensions |
|------|-------|------------|
| Flick | SphereShape3D | Radius: 0.4m |
| Cross Cut | CapsuleShape3D | Length: 1.2m, Radius: 0.2m |
| Overhead | CapsuleShape3D | Length: 1.0m, Radius: 0.25m |
| Lunge | BoxShape3D | 1.8m × 0.2m × 0.2m |

**Evasion counter**: Lunge loses to lateral Sidestep. Jump evasion is NOT effective.

### 8.2 Greatsword — "Commitment and Consequence"

The contrast archetype. Slower, more telegraphed, more punishing on both sides. Rewards correct reads over safe play.

| Attribute | Value |
|-----------|-------|
| Reach | Extended |
| Speed | Slow (all moves slower than Longsword equivalents) |
| Stagger Access | Universal |
| Momentum Curve | Volatile — "boom-and-bust." Fatigue is a constant threat |

#### Frame Data (60fps)

| Move | Tier | Coil | Swing | Recovery | Total |
|------|------|------|-------|----------|-------|
| Pommel Strike | 0 | 8 | 3 | 6 | 17 |
| Wide Slash | 1 | 14 | 6 | 10 | 30 |
| Crush | 2 | 24 | 8 | 16 | 48 |
| Cleave | 3 | 36 | 10 | 20 | 66 |

#### Damage Multipliers

| Move | Vitality | Composure | Notes |
|------|----------|-----------|-------|
| Pommel Strike | 0.3× | 0.15× (blocked only) | T0 conditional damage |
| Wide Slash | 1.4× | 1.2× | Higher Vitality than Cross Cut, lower Composure |
| Crush | 2.0× | 2.25× | Highest Composure multiplier in roster |
| Cleave | 2.75× | 2.75× | Symmetrical — pure power move |

#### Hitbox Shapes

| Move | Shape | Dimensions |
|------|-------|------------|
| Pommel Strike | SphereShape3D | Radius: 0.5m |
| Wide Slash | CapsuleShape3D | Length: 1.6m, Radius: 0.25m |
| Crush | CapsuleShape3D | Length: 1.2m, Radius: 0.3m |
| Cleave | Swept Area3D | Arc: 180°, Radius: 1.5m |

**Evasion counter**: Cleave loses to Jump. Lateral Sidestep is NOT effective (wide arc).

---

## 9. Input System

### 9.1 Input Mapping

| Action | Keyboard P1 | Keyboard P2 | Controller |
|--------|-------------|-------------|------------|
| `attack` | F | Numpad 5 | R1 / RB |
| `modifier_light` | G | Numpad 1 | X / Square |
| `modifier_heavy` | H | Numpad 2 | Y / Triangle |
| `modifier_super` | J | Numpad 3 | B / Circle |
| `jump` | Space | Numpad 0 | A / Cross |
| `block_parry` | Left Ctrl | Numpad Enter | L1 / LB |
| `dodge` | Left Alt | Numpad + | L2 / LT |
| `run` | Left Shift | Numpad * | R2 / RT |
| `move` | WASD | Arrow Keys | Left Stick |
| `camera` | Mouse | Mouse | Right Stick |

P2 actions use `_p2` suffix (e.g., `attack_p2`), set up by `GameCoordinator`.

### 9.2 Modifier Logic (Attack Tier Selection)

Modifier buttons determine attack tier at the moment `attack` is polled from the buffer:

| Modifier Held | Result |
|---------------|--------|
| None | Tier 1 (Standard) |
| `modifier_light` | Tier 0 (Flinchless) |
| `modifier_heavy` | Tier 2 (Heavy) |
| `modifier_super` | Tier 3 (Committed) |

If modifier is released before attack press, system reverts to Tier 1.

**Deferred**: `modifier_commitment_window` playtesting variable for optional commitment timing requirement.

### 9.3 Input Buffer (TTL Queue)

- 6-frame TTL at 60fps
- Each physics tick: Bridge polls hardware → enqueues pressed actions with TTL 6
- Each tick: decrement all TTLs, remove expired entries
- When state machine enters an actionable state: consume highest-priority buffered input
- **Priority order**: `block_parry` > `attack` > `dodge` > `jump` > `run`

---

## 10. Collision System

### 10.1 Collision Layers

| Layer | Purpose |
|-------|---------|
| 1 | Character Body — environment collision and pushback |
| 2 | Hitbox — active only during Swing phase |
| 3 | Hurtbox — active during Coil and Recovery phases |

`IntersectShape()` queries check Layer 2 against Layer 3.

### 10.2 Hit Resolution Order

Per tick during Bridge `_PhysicsProcess` (two-pass via `GameCoordinator`):
1. **Tick pass (both players)**: Poll input → build `PlayerInput` → call `Tick(input)` → apply movement
2. **Resolve pass (both players)**: If simulation state is `Swing`: run `IntersectShape()` query
3. Check defender state for blocking/parrying
4. Resolve damage, Composure, Momentum effects
5. Emit combat events to Presentation layer

**Two-pass architecture**: `GameCoordinator._PhysicsProcess` calls `TickPhase(delta)` on both players before calling `ResolvePhase()` on either. This ensures both players' state machines have advanced before any hitbox resolution, preventing tick-order asymmetry in clash detection.

---

## 11. Round Structure

| Property | Value |
|----------|-------|
| Lives per player | 4 |
| Round timer | 210 seconds |
| Round end: Deathblow | Composure ≥ 1.0 (or Vitality ≤ 0), any strike lands → receiver loses 1 life |
| Round end: Timeout | Higher Vitality wins, loser loses 1 life |
| On life loss | Full reset: positions, Vitality, Composure, Momentum, stacks all reset to initial values |
| Match end | One player reaches 0 lives |

---

## 12. Stage System

Functional sandboxes. Grid texture on flat floor plane. Boundary walls are invisible `CollisionShape3D` nodes with pushback force.

| Stage | Dimensions | Purpose |
|-------|------------|---------|
| Small | 10m × 10m | Close-range pressure and commitment testing |
| Medium | 20m × 20m | General balanced testing |
| Large | 35m × 35m | Running pressure and Right of Way generation |

---

## 13. Camera System

Split-screen with two `SubViewport` nodes (50% screen each), sharing the same `World3D`. Each viewport has its own `Camera3D` managed by `CameraController.cs`.

- Fixed offset behind and above the player's shoulder
- `SpringArm3D` prevents environment clipping
- Auto-rotation toward opponent using `Quaternion.Slerp`
- `camera_rotation_speed` = 5.0 (exported variable)
- Non-linear acceleration as opponent approaches viewport edge

---

## 14. UI / HUD

| Element | Display | Location |
|---------|---------|----------|
| Vitality Bar | Red continuous bar | Top of viewport |
| Composure Bar | Yellow-orange bar filling toward center | Below Vitality |
| Momentum Gauge | Segmented bar (8 segments) | Below Composure |
| Stack Counter | Numerical indicator | Near character model in 3D space |

**Deferred toggles** (not yet implemented):
- `momentum_ui_visibility` — control opponent visibility of Momentum gauge
- `stack_counter_position` — toggle between 3D world-space and HUD overlay

---

## 15. Exported Playtesting Variables

These variables are exposed in the Bridge Layer (Inspector-editable via `[Export]` on `PlayerBridge`) for real-time balancing without recompilation. Organized by `[ExportGroup]`:

**Economy:**
- `MomentumMax` (8.0)
- `PerfectParryCost` (0.5)
- `DodgeCost` (1.5)
- `JumpCost` (1.0)
- `ShatterCost` (3.0)
- `BaseMomentumOnHit` (0.5)
- `WalkMomentumRate` (0.05/frame) / `RunMomentumRate` (0.1/frame)
- `ClashMomentumSurge` (2.0)
- `ComposureRecoveryRate` (0.0036/frame)
- `ComposureRecoveryCooldownFrames` (90)
- `TerminalVitalityThreshold` (0.1)
- `BaseVitalityDamage` (0.08)
- `BaseComposureDamage` (0.06)
- `ArmorTradeLethality` (1.5)
- `StaggerKnockbackMultiplier` (0.2)
- `ChipDamageMultiplier` (0.2)
- `FrameAdvantageOffset` (1 frame per stack)
- `StackDecayDelayFrames` (180 = 3s)
- `StackDecayIntervalFrames` (60 = 1s)

**Frame Constants:**
- `ParryWindowFrames` (6)
- `DodgeStartupFrames` (3) / `DodgeActiveFrames` (12) / `DodgeRecoveryFrames` (3)
- `JumpStartupFrames` (3) / `JumpActiveFrames` (22) / `JumpRecoveryFrames` (5)
- `EvasionFatigueStartupPenalty` (4) / `EvasionFatigueActiveReduction` (4)
- `ClashRecoveryFrames` (8)
- `ShatterWhiffPenaltyFrames` (20)
- `InputBufferTTL` (6)

**Setup:**
- `Archetype` (Longsword / Greatsword)
- `WalkSpeed` (4.0) / `RunSpeed` (7.0)
- `BoundaryPushbackStrength` (5.0)
- `CameraPivot`, `ActiveHitboxManager`, `PlayerIndex`

---

## 16. Matchup Design Notes (Longsword vs. Greatsword)

This section captures intended competitive dynamics for playtesting reference.

### 16.1 Neutral Game

- Longsword controls neutral pace (faster strikes, lower recovery)
- Greatsword must force tempo shifts — whiffed/interrupted neutral strikes are more costly
- Flick (4f Coil) vs Pommel Strike (8f Coil): Longsword wins speed; Greatsword chips slightly harder
- Cross Cut (8f Coil) vs Wide Slash (14f Coil): Wide Slash hits harder but is more susceptible to Clash Window stuffing

### 16.2 Stack Economy Asymmetry

- Longsword accumulates stacks faster (shorter recovery → can re-enter parry timing more frequently)
- Greatsword at +3 stacks launching Wide Slash (14f Coil) is more easily interrupted than Longsword at +3 launching Cross Cut (8f Coil)
- Greatsword's ideal counter-strike at +3 is Crush (T2) rather than Wide Slash, but Crush's 24f Coil can still be stuffed

### 16.3 Tier 3 Evasion Read (Primary Skill Expression)

| Attack | Correct Evasion | Incorrect Evasion |
|--------|----------------|-------------------|
| Lunge (Longsword) | Lateral Sidestep | Jump (does not avoid) |
| Cleave (Greatsword) | Jump | Lateral Sidestep (walks into arc) |

Identifying the wind-up animation (Longsword's forward step vs Greatsword's diagonal weight shift) is the defining high-level skill of this matchup.

### 16.4 Armor Trade Math

Lunge vs Cleave simultaneous trade (both absorb 1.5× incoming):
- Longsword takes 2.75 × 1.5 = **4.125× effective Vitality**
- Greatsword takes 2.0 × 1.5 = **3.0× effective Vitality**
- Trade heavily favors Greatsword — Longsword should mathematically avoid

### 16.5 Shatter Asymmetry

- Longsword Shatter-modified Cross Cut: 8f Coil (fast, hard to react to)
- Greatsword Shatter-modified Crush: 24f Coil (more readable, but 2.25× Composure payoff)

### 16.6 Balance Flags (Playtesting Watch Items)

- **Armor Trade Dominance**: If Cleave vs Lunge math is too Greatsword-favorable, may only be used for net-positive trades
- **Crush Viability**: 24f Coil may be too easy for stack-rich Longsword to interrupt
- **Greatsword Fatigue Loop**: Slower Momentum generation → compounding disadvantage spiral
- **Shatter Asymmetry**: Longsword's 8f delivery may feel oppressive vs Greatsword's 24f

---

## 17. File Map

```
src/
├── simulation/                    # Pure C# — NO Godot imports. All classes sealed.
│   ├── PlayerSimulation.cs        # State machine, Tick(PlayerInput) entry point
│   ├── PlayerStates.cs            # Record types for all states
│   ├── EconomyHandler.cs          # Momentum/Composure/Vitality + stacks
│   ├── EconomyConstants.cs        # All balance + frame constants (Fixed64/int)
│   ├── InputBuffer.cs             # TTL queue with priority consumption (owned by Simulation)
│   ├── PlayerInput.cs             # Raw input struct (positions + hardware state)
│   ├── PlayerInputAction.cs       # Enum: buffered input actions
│   ├── AttackTier.cs              # Enum: Light/Standard/Heavy/Super
│   ├── ArchetypeType.cs           # Enum: Longsword/Greatsword
│   ├── MoveData.cs                # Readonly struct: complete per-move frame/damage data
│   ├── CombatEvent.cs             # Event enum for hit/parry/shatter/etc
│   └── archetypes/
│       ├── IArchetypeData.cs      # Interface: single GetMoveData(tier) → MoveData
│       ├── LongswordData.cs       # Longsword singleton (sealed)
│       └── GreatswordData.cs      # Greatsword singleton (sealed)
├── bridge/                        # Godot-aware conduit layer. All classes sealed.
│   ├── PlayerBridge.cs            # CharacterBody3D, raw input sampling, Tick() driver
│   ├── HitboxManager.cs          # IntersectShape() queries, combat resolution
│   ├── RoundManager.cs           # Lives, timer, deathblow, reset logic
│   ├── StageController.cs        # Stage sizing, boundary walls
│   └── archetypes/
│       ├── IArchetypeVisuals.cs   # Interface: hitbox shapes per tier
│       ├── LongswordVisuals.cs    # Longsword Godot shapes (singleton)
│       └── GreatswordVisuals.cs   # Greatsword Godot shapes (singleton)
├── presentation/                  # Observer-only display layer
│   ├── HUD.cs                    # Tweened bars, state labels, stack counter
│   └── DebugHUD.cs               # Debug overlay (F3 toggle): states, frame data, flags
└── scene/                         # Scene coordination
    ├── GameCoordinator.cs         # Wires P1↔P2, two-pass tick/resolve, registers P2 inputs
    ├── MainCoordinator.cs         # Entry point
    └── CameraController.cs        # Split-screen camera with opponent focus
tests/                             # GdUnit4 test suite (simulation layer coverage)
├── TestHelpers.cs                 # Shared factory methods for PlayerInput + sim helpers
├── InputBufferTests.cs            # TTL queue, priority, expiry (12 tests)
├── EconomyHandlerTests.cs         # Momentum/Composure/Vitality/stacks (27 tests)
├── PlayerSimulationStateTests.cs  # State machine transitions, action lock (24 tests)
├── AttackSystemTests.cs           # Coil→Swing→Recovery, armor, chip damage (33 tests)
├── EvasionSystemTests.cs          # Dodge/Jump 3-phase, fatigue, direction (28 tests)
├── ShatterClashDeathblowTests.cs  # Shatter/Clash/Deathblow/Parry events (31 tests)
└── ArchetypeDataTests.cs          # Frame data, multipliers, cross-archetype (39 tests)
```

---

## 18. Out of Scope (Phase 1)

- Online multiplayer / actual rollback netcode implementation
- Character selection screens or menus
- Final art assets, high-fidelity animations, spatial audio
- Third character archetype or AI opponents
- Ranking, progression, persistent player data

---

## 19. Phase 2 Preparation Checklist

Before implementing rollback netcode, verify:

1. **State Separation**: All combat state in pure C# — no Godot Node dependencies ✅
2. **Determinism**: All combat math uses `Fixed64` via FixedMathSharp ✅
3. **Unified Entry Point**: Simulation updated only via `Tick(PlayerInput)` ⚠️ *(partially — combat event handlers like `OnHitReceived`, `OnClash` are called by HitboxManager after `Tick()` returns; must be internalized for true rollback)*
4. **Input Buffer in Simulation**: InputBuffer owned by PlayerSimulation for snapshot/restore ✅
5. **Serialization**: Simulation state serializable to byte array (FixedMathSharp `MemoryPack` integration)
6. **Manual Collision**: Hit detection via `IntersectShape()`, not Area3D signals ✅
7. **Seeded RNG**: Visual variance uses `DeterministicRandom` from FixedMathSharp
8. **LogicBlocks Migration**: Migrate hand-rolled state machine to Chickensoft LogicBlocks for hierarchical states and native serialization support
