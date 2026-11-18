# Downhill running / scene m — 18 November 2026

Reference: `Screen Recording 2026-11-18 083656.mp4`; sampled frames: `../Downhill-20261118.png`.

The previous regression tests covered jumps, falls and sliding without sustained downhill input. Running at 4 m/s exposed repeated support loss: the input remained horizontal while the downward ground bias stayed fixed at 2 m/s. The steeper the descent, the more often the player walked off the surface. On the steep ramps this repeatedly restarted Land.

`PlayerScript` now projects grounded input onto the contact plane, preserves its vertical component alongside gravity, and follows small downward gaps with a bounded 0.25 m capsule-foot probe. Following only starts from existing support and is disabled during ascent, free fall and climbing/actions. A steep sliding contact returns to locomotion instead of starting a standing Land animation.

`Baseline.txt` contains the failing 30/60/120 FPS running cases before the fix. `Validation.txt` covers the same five slopes, four continuous routes sampled from the real m TerrainData, and small-step versus high-ledge behavior using the actual player Animator. The existing jump/wall/ceiling/fall/slide checks in `../Validation.txt` now call the production movement helper too.

No Play Mode, campaign writes, spawn changes or terrain/scene edits are used by these checks. Original scripts are in `Before/`.
