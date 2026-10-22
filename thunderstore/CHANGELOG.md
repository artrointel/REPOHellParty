# Changelog

## 0.1.0

- Rebind summoned enemy navigation to the NavMesh after initial spawn activation, bypassing the native near-distance Warp shortcut that could leave balloon Gnomes stationary.
- Tier-three damage-burst Gnomes now prioritize the damaged Birthday Boy's existing player target after native spawn initialization, including when the native Gnome group was attacking another target.
- Initial development scaffold: BepInEx entry point, Harmony initialization, build, debug, and packaging configuration.
- Added stage-based HellParty difficulty and Apex Predator clones/Crown.
- Added deterministic rule/assembly-contract tests. Removed the temporary duck playtest scenario and diagnostics.
- Added Animal target-gated poison gas, player-only host filtering, and a Debug-only Animal/weapon playtest scenario.
- Animal gas now requires its current target within 3m and has a 10-second cooldown. Difficulty 3 (stage 10) adds division into two half-health, 20-second-lived Animals with individual orb rewards.
- Delayed Animal division by 0.5 seconds so the killing shot does not immediately hit the newly spawned children; their lifetime still starts at actual spawn.
- Added Birthday Boy balloon proximity pops (3m/3s), Gnomes, tier-two aggro and newest-three death Eyeballs. Switched the Debug test scenario to Birthday Boy.
- Expanded tier-one Gnomes to player/physical pops with a living player within 10m. Moved balloon tier two to difficulty 2 and added tier three at difficulty 3: nonlethal body damage bursts nearby balloons into Gnomes; death loot takes priority on lethal hits.
