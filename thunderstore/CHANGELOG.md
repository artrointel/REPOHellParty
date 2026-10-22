# Changelog

## 0.1.0

- Share Chef body health and usable Frog durability by percentage, preserving fractional damage across transformations through native value RPCs. Environmental destruction also awards the native orb. Removed the disguise-specific 20m/raycast override from pursuit expiry; native pursuit now controls target release.

- Chef's disguise now ends only after five continuous seconds without a visible player. Reacquiring any player resets the timer; the one-second visibility scan interval is unchanged.

- Excluded Chef disguise Frogs from Gnome valuable selection and guarded drone attachment before native RPC emission. Chef now keeps its grabber target through attack recovery instead of redisguising on a temporary vision gap; target death or native pursuit expiry releases it.

- Chef now monitors player visibility every second while its native body is hidden. Losing all unobstructed living players within 20m removes the disguise and restores roaming without aggro or an orb, preserving health and grab/destruction precedence.

- Added difficulty-one Chef Mimic: 20m unobstructed roaming scans at one-second intervals, a destructible native Frog disguise that rejects drones, native orb rewards for breaking the disguise, grab release before revealing, complete native attack preparation with preserved health, and return to roaming after target loss. Switched the Debug scenario to Chef, weapons and a valuable-targeting drone.

- Bowtie-summoned Elsas now ignore their own summoner's native wind hit, including knockback/stun, while retaining the ten-second lifetime. Other Bowties, normal Elsas and players keep their native wind interactions.
- Added difficulty-four Bowtie Revenge: an Elsa is summoned at the saved death position after 0.5 seconds, targets the killing player when identified or the nearest living player, and uses the existing no-orb, ten-second animated summon lifecycle. No target means immediate animated retirement.
- Added difficulty-two Bowtie Brothers: native Elsa target inheritance and transformation, no orbs, a ten-second lifetime and animated retirement on timeout/replacement, with no automatic respawn. Excluded the proposed three-hit condition and switched the Debug scenario to Bowtie.

- Mounted Clown Doll explosions now use native enemyHost self-hit exclusion for their owning Bella only, preserving damage to other enemies and players.
- Bella bait now disappears silently at 15m from its living owner, becomes permanent loot if the owner dies while it remains inside that radius, and triggers immediate native attacks on every new grab, including repeated grabs of the same bait.
- Added Bella's native Clown Doll bell-linked suicide and difficulty-three Tiny bait, with silent per-Bella replacement, grabber-targeted native attacks, and collectible leftovers after death. Switched the Debug playtest scenario to Bella.

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
