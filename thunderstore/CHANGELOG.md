# Changelog

## 0.1.0

- Initial development scaffold: BepInEx entry point, Harmony initialization, build, debug, and packaging configuration.
- Added stage-based HellParty difficulty and Apex Predator clones/Crown.
- Added deterministic rule/assembly-contract tests. Removed the temporary duck playtest scenario and diagnostics.
- Added Animal target-gated poison gas, player-only host filtering, and a Debug-only Animal/weapon playtest scenario.
- Animal gas now requires its current target within 3m and has a 10-second cooldown. Difficulty 3 (stage 10) adds division into two half-health, 20-second-lived Animals with individual orb rewards.
- Delayed Animal division by 0.5 seconds so the killing shot does not immediately hit the newly spawned children; their lifetime still starts at actual spawn.
