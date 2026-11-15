# Discovery Area name reveal

The existing `CampaignSession` checks Discovery points whenever the Player enters their configured `range`, including points already discovered in the save. `WorldInteraction.CompleteCollection` grants/saves configured rewards and XP only if the area's consumed ID is not yet saved. Later eligible entries only announce `displayName`.

## Configure an area

1. Select the Discovery Area object. Set **Tên hiển thị** to the area's name.
2. Set **Khoảng cách tương tác** to the desired radius. The existing selection Gizmo shows this sphere.
3. Leave **Hiện tên khu vực** enabled.
4. Set **Hồi chiêu hiện tên (giây)**, default **30 seconds per area**. Set the area's experience reward to the amount granted on the first discovery. Newly created Discovery Areas default to 25 XP.

No additional collider or trigger is necessary for the existing radius-based discovery system. First-discovery XP/rewards remain one-time in the save. Leave the radius and re-enter after 30 seconds to show the title again; standing inside does not auto-repeat. An entry during cooldown is ignored and requires another exit/re-entry. Each area has its own unscaled-time cooldown. Session-owned state survives Canvas/chunk recreation; loading a different save starts a fresh entry/cooldown state.

## Configure the UI

Prefab: `Assets/Resources/Adventure/UI/AdventureCanvas.prefab`.

Hierarchy: `HUD / Discovery Area / Top Reveal / Divider` and `Area Name`. The bottom frame has been removed.

The `DiscoveryAreaBanner` component on the Canvas exposes:

- `Frame Reveal Seconds`: duration of the center-out ornamental reveal.
- `Title Delay Seconds`, `Title Fade Seconds`, `Title Rise`: title delay, fade and upward drift.
- `Hold Seconds`, `Fade Out Seconds`: visible duration and fade-away time.

The panel sits near the upper center of the view. Adjust its RectTransform to change its position. Font, color and size are editable on `Area Name`. The top divider references the existing sub-sprite from `Assets/asset/Ui/White Ornamental Divider Collection.png`; its artwork/import settings are preserved.

This is a non-interactive HUD: it does not pause the game, change movement, capture input or block pointer clicks. If a menu hides the HUD, the current title waits until exploration becomes visible again. Overlapping discoveries queue; duplicate callbacks while queued/playing do not restart the title. A later eligible entry can queue it again. Names use the existing localization system; add custom area names and translations to the localization table when needed.

## Native checks

**Tools > Adventure > UI > Setup Discovery Area Banner** authors/updates the prefab and creates a backup here. **Validate Discovery Area Banner** runs isolated Edit Mode previews and checks reveal, hold, fade, localization, queuing, replay, the exact 30-second boundary, independent area cooldowns, standing versus re-entry, streamed chunk recreation, old-save registration, the top frame only, and non-blocking UI. It never enters gameplay or writes player saves/map scenes.

The supplied video is inspected using `InspectVideo.ps1`; the contact sheet and timestamped preview PNGs are outside Assets and are not included in the build.
