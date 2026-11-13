CUSTOM FOOT TRAILS V5 — FOOT BONE FOLLOW FIX
================================================

1) Backup the current Assets/CustomFootTrails folder.
2) From this ZIP, copy Assets/CustomFootTrails into YOUR Unity project's Assets folder.
   Merge files, overwrite FootTrailController.cs, and keep existing .meta GUIDs.
   NOTE: If you have customized Generated prefabs/materials, back them up first.
3) The ZIP includes source, textures, shaders, editor generator and a V5 gameplay controller.
4) In the Player GameObject, add/keep FootTrailController.
5) Assign Left Effect + Right Effect to FT_... prefabs from Project OR two instances in the scene.
   Project prefabs are instantiated separately for the left and right foot at runtime.
   You may assign the same prefab to both fields; the controller creates two independent emitters.
6) Assign Left Foot Bone + Right Foot Bone from the animated skeleton.
   Humanoid Animator: Auto Find Humanoid Bones fills missing references on Play.
   It also replaces ik_foot helpers with the actual animated Humanoid foot_l/foot_r bones.
   IK helper bones may remain stationary in retargeted animations; use the actual feet.
   Generic rig: drag left/right foot bones manually in Inspector.
7) Keep Follow Foot Bones checked; tweak Left/Right Foot World Offset for sole height.
8) Movement Source = Player transform that actually moves in world space.
9) Automatic Footsteps ON, Meters Per Step 0.55, Minimum Speed 0.15.
10) Require Ground Contact OFF for first test; Debug Footsteps ON.
11) On Soft_Trail and Accents, keep Play On Awake OFF, Looping OFF, Rate Over Time 0.
12) Both ParticleSystem Main > Simulation Space = WORLD. This lets particles already
    spawned remain behind while the EMITTER follows the bone each frame.
13) Press Play and move the player. Check Console for LEFT/RIGHT BURST logs.
14) For more precise footfalls use Animation Events and Automatic Footsteps OFF.

IMPORTANT: Only effects (emitters) follow feet. Already spawned particles SHOULD NOT
follow the player, or you will lose the trailing dust effect.

TROUBLESHOOTING
- Emitter stays put: check left/right foot bone fields and follow checkbox.
- Effects spawn in wrong spot: adjust Foot World Offset (usually y -0.05 to -0.15).
- No bursts: check Moving Source, both Effect fields, the ground-check toggle.
- Logs show BURST but no visible particle: check material/shader, camera and particle size.
- To try an existing project, you can replace ONLY the controller script rather than
  importing the whole package. Do not keep two controllers with the same class name.

Project fix verified in Unity with the actual player (3) prefab and BattleEntryWalk:
separate runtime emitters, actual Humanoid feet, world-space particles, URP materials,
idle/teleport suppression, manual events, disable/re-enable cleanup and existing entry-walk regressions.
Validation: Tools > Adventure > Combat > Validate Foot Trails During Entry Walk.
