# Tide & Till

**Tide & Till** is an original HD 3D farming/life-sim vertical slice built in Unity 6 URP. It captures the cozy rhythm of the genre—farm work, an island village, changing light, weather, and friendly characters—using entirely original names, code, world design, and procedural art.

> This project is genre-inspired, not a reproduction of *Coral Island*. It does not include copied characters, maps, story, branding, models, textures, audio, or source code.

## Play it

1. Open the repository in **Unity 6000.3.2f1**.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press **Play**.

The game bootstraps itself at runtime and builds Sunpetal Island procedurally; there are no prefab or asset-store dependencies.

## Controls

| Input | Action |
|---|---|
| WASD / arrow keys | Move |
| Left Shift | Sprint |
| Space / left click | Use selected tool |
| E | Interact / talk |
| 1–5 / mouse wheel | Select or cycle tools |
| Right mouse drag / right stick | Orbit camera |
| `+` / `-` | Camera zoom |

## Playable loop

1. Equip the **hoe** and till a green farm plot.
2. Select **seeds** and plant a moonmelon.
3. Refill at the stone well, then **water** the crop.
4. Interact with the cottage door to sleep. Watered crops advance each morning; rain waters all tilled plots.
5. Use the **harvest basket** when moonmelons turn gold.
6. Ship produce in the teal farm crate or buy more seeds at the coral village stall.
7. Equip the **axe** to clear old driftwood stumps.

A mature moonmelon is planted in the starting garden so the complete harvest-and-ship loop can be tested immediately.

## Prototype features

- Original, explorable tropical island with a farmstead, village, harbor, ferry, lighthouse, beaches, palms, paths, props, and NPC
- Camera-relative third-person movement, sprint energy, smooth follow/orbit camera, and gamepad support
- 35-plot farming grid with tilling, watering, planting, staged crop growth, harvesting, produce, seeds, and currency
- Five-tool quick bar and proximity interactions
- Accelerated day/night lighting, golden hour, night ambience, deterministic rain, and rain particles
- Runtime-built, resolution-aware HUD with journal objective, clock, weather, inventory, stamina, water, prompts, and notifications
- Custom URP shaders for vertex-colored island terrain, animated foliage, and layered moving ocean water
- Procedural stylized models and materials—no external art packages required
- Lightweight prototype save schema for future persistence work

## Code map

```text
Assets/TideAndTill/
├── Resources/                    # Ground, foliage, and water shaders
└── Scripts/
    ├── Core/GameState.cs         # Inventory, currency, stamina, quest state
    ├── Farming/FarmSystem.cs     # Plot and crop simulation
    ├── Input/GameInput.cs        # Keyboard/mouse and gamepad actions
    ├── Player/PlayerController.cs# Movement, tool use, procedural avatar, camera
    ├── Systems/DayNightSystem.cs # Clock, lighting, weather, save schema
    ├── UI/GameHUD.cs             # Runtime uGUI presentation
    ├── World/Interactables.cs    # Well, bed, market, NPC, crate, stumps
    ├── World/WorldBuilder.cs     # Procedural island and material library
    └── TideAndTillBootstrap.cs   # One-click runtime composition
```

## Verification

`Tools/verify_terrain_grounding.py` is a headless check for the two invariants the
island depends on: terrain triangles must face up, and grounding recovery must stay
stable. It reads the triangle winding straight out of `WorldBuilder.cs`, rebuilds the
mesh heights from a Python port of `SampleHeight`, and then re-uses the
`PlayerController` grounding contract to confirm the player can find ground, lands,
settles, and recovers. It needs no Unity install:

```bash
python3 Tools/verify_terrain_grounding.py
```

It runs in a few seconds and exits non-zero on failure, so it is usable as a CI gate.

## Suggested next production steps

1. Replace procedural proxy models with an original authored art kit and animation rig.
2. Move item/crop/tool definitions into ScriptableObjects.
3. Add full farm/world serialization and multiple save slots.
4. Add interiors, fishing, diving, relationship schedules, quests, sound, and accessibility settings.
5. Profile on target hardware, add LOD groups/occlusion, and bake production lighting.

## Technical notes

- Render pipeline: Universal Render Pipeline 17.3
- Input: Unity Input System 1.17
- Color space: Linear
- Target aspect: 16:9, responsive HUD
- Build scene: `Assets/Scenes/SampleScene.unity`
