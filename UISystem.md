## Phase 0 — UI framework foundations

Right now your only overlay is the context menu, drawn ad-hoc inside `WorldRenderer.Draw` with boolean state on `ContextMenuController`. Before adding four more panels, add a thin screen abstraction so they don't each reinvent layout/hit-testing/input-blocking.

1. In `Game.Client.UI`, add `IUiScreen` with something like `bool IsModal`, `void HandleInput(InputFrame frame)`, `void Draw(...)`, and a `UiScreenStack` that holds the active modal screens (top screen gets input). Keep the HUD/side menu separate from this stack — it's always-on, not modal.
2. Extract the shared drawing primitives (`DrawRect`, `DrawBorder`, text) out of `WorldRenderer` into a small `UiPainter` that both the world renderer and screens share. `WorldRenderer` already owns `_pixel`, `_font`, `_spriteBatch` — pass those in rather than duplicating.
3. Extend `RenderSnapshot` with display-only sub-records: a `PlayerStatus` (HP/MaxHP, energy, speed, world+local position, current biome/terrain) and views of inventory and quests. Populate them in both `BuildOverworldSnapshot` and `BuildLocalMapSnapshot` in `SimulationHost`, reading from `Session.PlayerEntity`, `Session.Inventory`, `Session.QuestLog`, `Session.PlayerTurnState`. This keeps the renderer reading from one source and stays consistent with your existing pattern rather than having panels reach into the session directly.
4. Wire a `UiManager` into `BlueHarvestGame.Update`: sample input → offer it to the UI stack first → if a modal screen is open, **skip `HandleSimulationActions`** (mirror the existing `menuWasOpen` guard you already use for the context menu). Draw the UI after the world in `Draw`.

## Phase 1 — ESC pause menu (do this first; it's smallest and shakes out the framework)

There's a conflict to resolve here. `InputAction.LeaveLocalMap` currently doubles as "leave local map" and "save + quit from overworld" (see `HandleSimulationActions`). If ESC becomes pause, that behavior needs to move.

1. Add `InputAction.OpenPauseMenu` and a `controls.yaml` binding. (Note: adding an enum value without a binding is safe — `InputMapper` skips unmapped names and `ContentLoader.Validate` only requires that *bound* actions have a key, not that every action is bound.)
2. Build `PauseMenuScreen : IUiScreen` (modal). Options: Resume, Save, Leave Local Map (only shown in `GameViewMode.LocalMap`), Settings (placeholder), Quit to Desktop.
3. Reuse `GameBootstrap.SaveGame(...)` and `Exit()` for Save/Quit — no new sim plumbing needed. Move the old overworld save-and-exit path into this menu.

## Phase 2 — Side menu / HUD

A persistent status panel replacing (or absorbing) the current debug-text block in `WorldRenderer`. Show HP bar, energy/speed, position, biome/terrain, a compact quest summary, and buttons for Inventory/Character/Menu.

One decision to make up front: **overlay vs. reserved space.** `CameraController.CenterOn`, `GridPicker.TryScreenToGrid`, and the visible-range math in `WorldRenderer.Draw` all assume the world fills the whole 1280×720 back buffer. If the sidebar *reserves* screen space you have to thread a "world viewport rect" through all three. My recommendation for the first pass: draw the sidebar as an opaque overlay and add a hit-test that swallows clicks landing inside its rect (so a click on the panel doesn't also register as a move-to on the world underneath). You can graduate to a true reserved viewport later if you want the world un-occluded.

## Phase 3 — Inventory screen

1. `InventoryScreen : IUiScreen` reading the inventory view off the snapshot.
2. You'll need display names — `ItemId` only has `None/Wood/Berry` with no metadata. Start with a static `ItemId → name` map; promote to an `items.yaml` content file later (mirroring how `quests.yaml` feeds `QuestsDefinition`) if items grow.
3. Toggle via a new `OpenInventory` action and via the side-menu button.
4. Keep *viewing* client-side. If/when you add Use/Drop, those become new `GameIntent` values + `GameSession` methods so the sim stays authoritative.

## Phase 4 — Character sheet

`CharacterSheetScreen` showing HP/MaxHP, energy/speed, faction, world+local position, inventory totals, and quest progress (join `QuestLog.Progress` with `GameContentBundle.Quests` by id to get titles/objectives — the log only stores id/state/progress).

Flag: your `Entity` model is thin (no level, XP, or attributes), so the sheet can only show what exists today. Decide now whether the sheet is a driver to extend the model (Strength/Level/XP on `Entity` or a new `CharacterStats`) or just surfaces current fields. That choice affects save format (`WorldSaveData`/`EntitySaveData`) and is worth settling before you build the panel.

## Phase 5 — Polish

- `RenderSnapshot.HoverTooltip` already exists and is always passed `null` — it's a ready-made hook for tile/entity tooltips.
- Centralize colors. Some live in `CameraDefinition` (SelectionColor, PlayerColor); consider a `UiTheme` content file so panels aren't hardcoding hex like `WorldRenderer` does today.
- Standardize close/back semantics (ESC pops the top modal; a toggle key opens/closes its own panel).
- Update the on-screen controls hint string in `WorldRenderer.Draw` for the new keys.

## Cross-cutting notes

- Keep UI *logic* (layout, hit-testing, stack transitions) in plain classes like `ContextMenuController` already is, with only drawing depending on MonoGame — that keeps it unit-testable headless. `ContextMenuController.Layout`'s use of `SpriteFont.MeasureString` is the one MonoGame dependency; consider abstracting measurement behind an interface if you want those tests.
- `RenderSnapshot` is cached and only rebuilt when `Session.RenderDirty`. Opening/closing a panel doesn't mark it dirty, so if HUD data needs to feel live (energy recovering), either read status independent of the cached grid snapshot or mark dirty on UI events. Minor, but it'll bite if missed.