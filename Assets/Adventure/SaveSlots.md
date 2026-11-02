# Save and load slots

- Rest point → Rest / Save → choose a slot → Yes writes a snapshot and restores party HP.
- Main menu (P) → Load Game → choose an occupied slot → Yes reloads its map and campaign state.
- No returns to the slot list. Escape/P closes confirmation first, then the list.
- Every occupied slot shows its saved date/time in the machine's local time zone,
  location and accumulated play time as `HH:mm:ss` (hours can exceed 24).
- `CampaignConfig → Save Slots → Save Slot Count` controls the list (default 10, range 1–99).
  Reducing the count hides higher slots; it does not delete their files.

## Edit the Canvas

In `Assets/Resources/Adventure/UI/AdventureCanvas.prefab`:

- `Menu/Save Slots/List Panel` owns the list title, list and Back button.
- `Slot List/Content/Template` is the row template. Enable it temporarily in Edit Mode
  to edit `Saved At`, `Location`, `Play Time Label` and `Play Time`.
- `Menu/Save Slot Confirmation/Confirmation Panel` owns the question and Yes/No buttons.
- `Menu/Party/Overview/LoadGame` is the new main-menu action.
- `AdventureSaveSlotsView` on the Canvas exposes references, sprites and fallback row sizing.
  Existing authored RectTransforms are reused at runtime.
  Slot rows and confirmation buttons keep a fixed scale/background; their text is gray
  normally and white only while the pointer is over the button.

Use `Tools → Adventure → UI → Setup Save and Load Slots` only to add missing UI.
Setup makes a backup of the current prefab and preserves existing elements/layouts.

## Files and compatibility

Manual snapshots are `adventure-slot-01.json`, `adventure-slot-02.json`, etc. under
`Application.persistentDataPath`. The previous version of an overwritten slot is kept
as `.bak`. Automatic progress still uses the separate `adventure-save-v1.json` file.
Automatic saves never overwrite a manual slot. Existing automatic saves remain compatible.
Old saves have no historical play-time counter, so their counter begins with this update.

Every manual snapshot includes party, items, quest/dialogue/world progress, checkpoint,
current map and player pose. Loading the current map also reloads its scene to restore
world objects correctly. Empty/corrupt slots cannot be loaded; the UI reports errors.

## Verification

- Runtime and Editor assembly compilation passed.
- Storage passed 33 isolated file tests in a temporary directory, including overwrite,
  independent slots, corruption, write failures and autosave preservation.
- Unity Editor setup/validation passed: all authored references, cancel-to-list,
  play-time formatting above 24 hours and preserving RectTransforms on repeat setup.
- No live player saves were created or loaded for validation.
