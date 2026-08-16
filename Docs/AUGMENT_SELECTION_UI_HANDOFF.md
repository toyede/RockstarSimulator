# Augment Selection UI Handoff

## Purpose

This implementation owns only the **Augment selection screen and its input contract**.
It deliberately does not decide which Augments appear, their rarity, duplicate rules,
reroll costs, effect application, or persistence.

Current tour flow integration:

```text
Performance result confirmed
-> RunPhase.Reward
-> Augment selection popup
-> one Augment selected
-> TourRunManager.SelectAugment(id)
-> RunPhase.Map
```

## Keep: reusable View layer

These files are the stable UI contract and can be reused with the final art prefab:

- `Assets/Scripts/UI/Augment/AugmentSelectionViewModels.cs`
- `Assets/Scripts/UI/Augment/AugmentChoiceView.cs`
- `Assets/Scripts/UI/Augment/AugmentSelectionPopup.cs`

The popup receives display-only models and emits slot-based requests:

```csharp
popup.Show(screenModel);
popup.UpdateChoice(choiceModel);
popup.SetBusy(trueOrFalse);
popup.ShowError(message);
popup.Hide();

popup.SelectRequested += HandleSelectRequested;   // int slotIndex
popup.RerollRequested += HandleRerollRequested;   // int slotIndex
```

The UI never receives an Augment ID and never mutates the run, deck, or player state.
It only knows that three visible slots exist.

## Replace: temporary prototype adapter

`Assets/Scripts/Tour/PrototypeAugmentSelectionAdapter.cs` exists only to make the
vertical slice playable before the real Augment system is ready. It contains three
sample offers and deterministic replacement samples.

Replace this class with the real Augment Coordinator. Do not move its sample data or
rules into the View.

The real Coordinator should:

1. Read the current reward context, including `StageDefinition.RewardTableId`.
2. Ask the Augment offer service for three candidates.
3. Convert candidates to `AugmentSelectionScreenModel` and call `popup.Show(...)`.
4. Keep the mapping from `slotIndex` to the real Augment ID internally.
5. On `RerollRequested(slotIndex)`, validate and consume only that slot's reroll.
6. Generate one replacement candidate and call `popup.UpdateChoice(...)`.
7. On `SelectRequested(slotIndex)`, validate and apply the chosen Augment.
8. Only after successful application, call `TourRunManager.SelectAugment(realId)`.
9. On failure, call `popup.ShowError(...)`; on success, hide the popup.

Per-slot reroll counts belong to the real system/Coordinator, not the View. The View
only displays the supplied `rerollsRemaining` value. This allows future Augments to
grant extra rerolls without changing UI code.

## Current prototype visuals

`Assets/Scripts/Tour/PrototypeAugmentSelectionUIFactory.cs` builds the current popup
at runtime so the whole tour can be tested before final assets arrive. It is presentation
scaffolding only.

When the final art prefab is ready:

1. Add `AugmentSelectionPopup` to the popup root.
2. Add one `AugmentChoiceView` to each of the three card roots.
3. Assign the serialized Text, Image, and Button references.
4. Replace the runtime factory call in `TourPrototypeUI` with the prefab instance.
5. Keep the same View events and methods, so the real Coordinator does not change.

## Responsibility boundary

UI team owns:

- Popup layout and animation
- Three visible choice slots
- Icon, tier, name, description, owned count, and remaining-reroll display
- Select/reroll button input and disabled/busy/error presentation

Augment system owner owns:

- Augment definitions and IDs
- Tier probabilities and reward tables
- Candidate generation and duplicate prevention
- Per-slot reroll allowances and consumption
- Augment effect validation/application
- Saving/loading owned Augments
- Any card/deck/player-state mutation

Do not implement domain rules in `AugmentSelectionPopup` or `AugmentChoiceView`.
