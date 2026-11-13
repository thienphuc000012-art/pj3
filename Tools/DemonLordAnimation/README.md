# DemonLord animation repeat fix

- Enemy prefab: Assets/Adventure/Prefabs/Enemy/DemonLord/DemonLord.prefab, also used by Enemy Encounter in mapgame.
- Turned off Loop Time on 2HitCombo, 3HitCombo, SwordAttack1, SwordAttack2, WhipAttack, GetHit1, GetHit2, Roar. Kept animation curves and all event timestamps unchanged.
- SwordAttack1 / SwordAttack2 / WhipAttack -> JumpBack now use Has Exit Time, Exit Time 1. Their old unconditional transitions exited immediately and could skip damage/end markers.
- Idle, Walk, Fly, FlyBackwards keep looping.
- BattleStart and the rest of the combat logic are unchanged.

Tools > Adventure > Combat > Validate DemonLord Single Play Animations samples the actual prefab/controller in an isolated Editor scene. The report checks intro plays once, action playback reaches all hit/end timestamps, JumpBack returns to Idle, and the final pose of a non-looping combo holds instead of restarting. This validation does not enter Play Mode, apply gameplay damage or write saves. Native AnimationEvent callback delivery is not tested outside Play Mode.

Validation.txt contains the final result. Changes.json points to the original asset backups.
