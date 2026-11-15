# Terrain jumping and slope sliding

Reference: `Screen Recording 2026-11-15 130322.mp4` (14 seconds). `ReferenceVideo.png` contains sampled frames.

The former ground check combined the CharacterController's previous grounded flag with an overlap sphere. A steep bank or side wall inside the sphere could count as ground. While that flag remained true during ascent, the movement branch did not subtract gravity, and could also permit another jump from a side contact.

`PlayerScript.cs` clears grounding at takeoff, rejects grounding while rising, and always applies gravity to positive vertical speed. Ground checks look downward for foot contact. A walkable contact enables jumping (`onSurface`); steeper upward-facing contact supports sliding and ends Fall (`HasGroundSupport`) while leaving jump permission false. Vertical walls do not count as foot support. The probe radius follows the controller's bottom hemisphere, and its starting height clears the controller skin on steep banks.

Automatic sliding begins when the surface exceeds CharacterController **Slope Limit** (currently 50 degrees). Its direction is gravity projected onto the actual terrain contact plane. The slide accelerates to a bounded speed and stops immediately on walkable ground or loss of steep support. Input can steer sideways/downhill; its uphill component is removed on a sliding face. Positive jump velocity always receives gravity.

Player → **Player Script → Slope Sliding**:

- **Slope Slide Speed**: default **3 m/s**, measured along the surface. Set 0 to disable automatic sliding.
- **Slope Slide Acceleration**: default **8 m/s²**. Higher values reach the maximum speed sooner.
- **CharacterController → Slope Limit** controls which slopes trigger sliding; slopes within this limit remain walkable.

Sliding is added separately from movement input, so releasing the controls still allows a steep downhill slide. Airborne motion has no automatic camera/forward drive. Fall/landing tracking and the Animator's OnSurface flag use physical foot support, so sliding does not get stuck in Fall. Space retains the walkable-slope requirement for a regular jump.

Supported-air recovery handles both **Jump** and **fallingidle**. It lets an existing transition toward Basic Movement or Land finish instead of restarting that blend every frame. A soft landing exits an air pose as soon as support returns; a genuine high fall keeps its Land animation. Pending Jump/Land triggers are cleared only when recovering an obsolete air pose. This also handles Jump waiting for negative vertical speed while sliding holds vertical speed at zero.

Jump Force remains 5, gravity remains unchanged, and no Player transform, prefab, terrain height, spawn or scene file was edited by this correction. Space remains the jump/climb key.

Unity menu **Tools → Adventure → Climbing → Validate Terrain Jump** runs isolated CharacterController/Physics checks without Play Mode or campaign saves. It covers takeoff, stale ground flags, walls, triggers, walkable/steep slopes, movement along slopes, 30/60/120 FPS trajectories, and eight travel directions on a read-only clone of Terrain Main at the current Player location. It also steps the real player Avatar/Animator and production grounding, landing, gravity and slide logic **every frame** for complete Jump → slope slide → ground and 4m fall → Land → ground sequences at all three frame rates. `Validation.txt` contains the latest measurements. These are isolated Edit Mode checks, not a recorded Play Mode retest.

At the sampled spot, directions toward the bank can end against roughly 62-degree ground while the controller's slope limit is 50 degrees. These contacts now slide downhill and end Fall without enabling another jump. Actual controller support on a walkable solid floor outside the configured probe mask is still recognized. The later reference recording is `Screen Recording 2026-11-15 132412.mp4`, with sampled frames in `SlopeFall-132412.png`.

The original player script is preserved in `Before-20261115-130748690`. Scene/TerrainData hashes were recorded there before the change. Current unsaved scene work is left untouched.

Validation: 15 November 2026, 13:47. Flat and wall-adjacent jump apex: 1.192 m at 30 FPS, 1.233 m at 60 FPS, 1.253 m at 120 FPS, with no rising grounded frames. On a 70-degree slope, a two-second slide descended approximately 5.10–5.13 m at those frame rates, with peak speed 3 m/s. Native checks also cover downhill direction, tangent-plane movement, uphill input rejection, stop on flat ground, Speed=0 disable, actual m terrain routes and actual player Animator Fall recovery. Details are in `Validation.txt`. The previous no-slide behavior and report are backed up in `Before-Sliding-20261115-134241307`.

Animation regression validation: 15 November 2026, 14:04. Before correction, repeated recovery calls left Jump frozen at normalized time 5 and left Fall continuously restarting its exit blend. This is archived in `Before-SlideAnimation-20261115-135518895/Baseline-RepeatedAirRecovery.txt`. After correction, repeated calls recover both states. Complete physics/Animator cycles at 30/60/120 FPS end in Basic Movement, with one Jump entry, under 0.08 seconds of supported air pose, and intact Land playback on the 4m fall. Reference video: `Screen Recording 2026-11-15 135115.mp4`; sampled frames: `SlideAnimation-135115.png`.

At 14:06 the full ledge-route checks also passed after the animation recovery correction: Grab, Hop Up/Down, Shimmy limb clearance, Space handling and climb6-to-roof completion. `Tools/Climbing/Validation.txt` is the current report. Scene m and TerrainData hashes match the record made before this change.
