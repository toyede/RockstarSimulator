# RockstarSimulator Agent Guide

This file applies to the entire repository.

## Project Overview

- Project: RockstarSimulator
- Engine: Unity 6000.3.20f1
- Render pipeline: Unity 6 URP 2D
- Input: Unity Input System
- Main scene: `Assets/Scenes/SampleScene.unity`
- Project namespace: `ContextStage`
- Shared framework namespace: `GameJamKit`
- Unity integration: official `unity` CLI with `com.unity.pipeline`
- Unity MCP is not required.

## Main Paths

- `Assets/GameJamKit/`: reusable game-jam framework
- `Assets/GameJamKit/README.md`: framework documentation and change history
- `Assets/Scripts/Hype/`: hype runtime system
- `Assets/Scripts/Cards/`: card data, hand, input, and UI
- `Assets/Scripts/Editor/`: project-specific setup tools
- `Assets/Settings/`: gameplay configuration assets
- `Assets/Scenes/SampleScene.unity`: current playable scene

## Current Scene Structure

`SampleScene` currently contains:

- `[Managers]`: GameManager, AudioManager, PoolManager, UIManager, CameraShake
- `[HypeSystem]`: HypeSystem and HypeDebugInput
- `[CardSystem]`: CardSystem and CardInput
- `HypeCanvas`: hype gauge and game-over popup
- `CardCanvas`: bottom card hand and number-key hint

The hype and card systems use the GameManager state machine and EventBus.
Cards are selected by the number shown in the current hand.

`SoundLibrary.asset` and `ColorPalette.asset` are not currently present.
Do not assume that audio IDs are configured only because audio files exist.

## Design Documentation Policy

Do not store detailed balance values or evolving gameplay rules in this file.
Keep balance data in ScriptableObject assets.
Use this file for workflow, implementation conventions, safety rules, and
verification requirements.

## Minimum-Change Policy

1. Inspect the relevant scripts and Git status before making changes.
2. Extend the existing implementation instead of replacing unrelated systems.
3. Modify the minimum number of files required.
4. Do not regenerate scenes, prefabs, or configuration assets unnecessarily.
5. Preserve existing GUIDs and serialized references.
6. Explain any required structural change before implementing it.

## Protected Paths

Do not modify these unless the task explicitly requires it:

- `Packages/`
- `ProjectSettings/`
- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- generated solution and project files

## GameJamKit Conventions

- Review `Assets/GameJamKit/README.md` before adding a system already covered by the kit.
- Keep reusable systems in `Assets/GameJamKit`.
- Keep game-specific systems in `Assets/Scripts` under `ContextStage`.
- Prefer EventBus for communication between independent systems.
- Subscribe in `OnEnable` and unsubscribe in `OnDisable`.
- Use `MonoSingleton<T>.OnAwake()` for singleton initialization.
- Use `PoolManager.Spawn` and `Despawn` for frequently reused gameplay objects.
- Keep balance values in ScriptableObject assets.
- Prefer existing facades such as `Sound` and `Hype` over unnecessary scene references.
- Record intentional GameJamKit changes in its README change history.

## Coding Rules

- Prefer `[SerializeField] private` for Inspector references.
- Avoid expensive lookups and allocations in `Update()`.
- Never call scene-wide object searches every frame.
- Keep components focused on one responsibility.
- Pair every EventBus subscription with an unsubscribe operation.
- Use UTF-8 for source files.
- Code comments may be written in Korean, but keep them concise and focused on intent.
- Keep UI-facing Korean text easy to move into a localization layer later.

## Unity Asset Safety

1. Run `git status --short --untracked-files=all` before changing files.
2. Inspect existing changes in every target file.
3. Inspect the hierarchy before modifying scenes or prefabs.
4. Use official Unity Pipeline commands for scene objects, components,
   serialized references, and asset operations.
5. Do not edit Unity YAML manually.
6. Do not manually delete or regenerate `.meta` files.
7. Preserve manual layout, camera, Inspector, and hierarchy changes.
8. Treat Play Mode changes as temporary unless they are explicitly saved in Edit Mode.

## Unity Workflow

- Run commands from the repository root.
- Prefer JSON output where the command supports it.
- The user keeps the Unity Editor open. Never launch or reopen the Editor.
- Connect only to the existing Editor instance. If it is unavailable, report the connection state instead of opening Unity.
- Do not enter Play Mode for file-only investigation.
- Inspect command schemas instead of guessing arguments.
- Prefer typed Pipeline commands over `eval`.
- Use `eval` only for narrow diagnostics or operations not covered by a typed command.
- Save scene changes explicitly and inspect the resulting Git diff.

## Compile Verification

After changing Unity scripts:

```powershell
unity --non-interactive --format json command --project-path . recompile
unity --non-interactive --format json command --project-path . recompile_status
unity --non-interactive --format json command --project-path . get_console_logs --severity error --limit 100
```

Unity compilation and the Unity Console are the source of truth.
Do not use `dotnet build` as the primary Unity verification method.
Run relevant Unity tests when they exist.

## Completion Checklist

- Unity compilation succeeds.
- The Unity Console contains no new errors.
- Scene and asset references remain valid.
- Relevant gameplay transitions are tested.
- EventBus subscriptions and state transitions remain balanced.
- No unrelated files are changed.
- The final response lists modified files, test results, assumptions, and unverified behavior.
