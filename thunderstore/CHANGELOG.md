# Changelog

## Unreleased

- Add tier-4 Headman Protection: idle Headmen inherit nearby allies' player targets or identified attackers within 20m; borrowed Reaper bodies are excluded. Switch the temporary test to maximum-tier Headman with a vanilla Animal ally and two guns.
- Add tier-2 Trudge Muster (소집): every ground slam summons two random native Tick/Gnome/Banger after one second, sharing the last target. No HellParty abilities, timed retirement or post-death respawn; native Banger detonation remains.
- Verify Huntsman and Loom share one global abduction reservation and reject new captures while anyone is already captive.

- Implement Clown tier-3 Laser Staff Summon and tier-5 Laser Shield. Shield blocks gun damage/stun while targetless and fires a fixed-aim Staff after two seconds. Surviving staves become collectible loot on owner death. Prepare exclusive difficulty-5 Debug.Clown with shotgun and tranquilizer.
- Add Loom tier-4 Abduction using the Huntsman shared rules and global captive slot. Both check a clear native vision ray immediately before capture.
- Remove Birthday Boy Eyeball generation entirely; death balloon pops have no extra loot or Gnomes.
- Replace Reaper Blind with tier-2 Sprint: transform into native Headman at 10m target distance, return to Reaper within 3m, ten-second use cooldown, shared health and original Reaper core rules. Retain Blind on Shadow Child only. The selectable Reaper debug scenario uses difficulty 2.

- Advance normal difficulty by one tier: stages 1–3 / 4–6 / 7–9 / 10–12 / 13+ now use tiers 1 / 2 / 3 / 4 / 5. Preserve difficulty 0 as a vanilla baseline and existing Debug scenario overrides.
- Refresh repository and package documentation, stage unlock labels, implementation status and profile-driven Debug instructions.
- Document current Huntsman Abduction: 7m range, 2 damage every second after a three-second delay, no shooting while carrying, captive sound filtering, carrier-grab rejection, and a five-second release cooldown. Add tier-4 Death Tactics with a native recoverable Human Grenade.
- Add Cleanup Crew Hatching/Hatching2 and natural Upscream Big-valuable disguises; ability/wave-summoned Upscreams are excluded from disguise.
- Add Reaper/Shadow Child Blind: native gas at the target's eye position every three seconds of continuous targeting; target changes/loss reset the timer.
- Update Monster Wave: 20m radius, three-second posture-based evasion warning, two Gnomes per second, twenty-second total lifetime, one-second retries, revalidated spawn-position caches, nearest-player candidate ordering, and a strict greater-than-three-metre player clearance. Break the Gramophone through native effects on combat end; cancel warnings silently.

## 0.1.0

- Mentalist Reflection Shield now always retaliates with a native laser gun regardless of the incoming gun. Aim locks on creation, laser firing starts after two seconds, and native laser damage/duration are retained.

- Mentalist gains Reflection Shield at difficulty 2 while it has no player target. Native gun hits have damage/stun blocked; one matching temporary gun per shot locks aim immediately, fires through native RPCs after two seconds, then disappears. Shotgun pellets share one retaliation. Reflected shots cannot chain reflections.

- Reduced Stalking Doll health to 100 and kept displayed price proportional to remaining HP / 100.
- Peeper summons a Gnome on each confirmed damaging hit from difficulty 1, with a 30% bonus Banger chance from difficulty 2. Both types share a cumulative ten-unit limit per Peeper. Summons inherit the snapshotted victim through native group AI; overlapping remote damage has attribution limitations.

- From difficulty 1, native Loom and Robe spawns each bring one Stalking Doll companion per instance per stage. Rage is innate; difficulty 3 adds Rampage, triggering that same rage on a confirmed normal attack regardless of direction. Grab retaliation does not trigger Rampage. Player-death and 30-second inactivity exits remain.

- Added Stalking Doll rage: three accepted grab attempts shared across players trigger triple-speed pursuit that ignores observation and keeps native attacks active while moving. Any confirmed player death or 30 seconds without a valid hit ends rage and resets its counter. With no hits, the timer starts at rage entry. Remote hit confirmation correlates pending attacks with native health decreases; overlapping damage cannot be attributed exactly by vanilla messages. Repeated death messages do not reset a later activation.

- Stalking Doll attacks now total 10 player damage using the native hit plus a host-sent native health supplement. Grab attempts deal 10 damage to the grabber and cancel the grab, with duplicate request suppression. Wall/floor collisions no longer damage its health or value; weapon and thrown-object damage remain. Multiplayer hit alignment still requires playtesting.

- Stalking Doll price now follows original native value multiplied by remaining HP / 200. Removed host-only price/discovery hiding; host and vanilla guests use the same native price UI and impact feedback without monster red flashes. Extraction remains disabled, and native 15% value thresholds do not kill a living doll.

- Added 200 HP to Stalking Dolls, routed native enemy weapon damage and environmental impacts to health, excluded dolls from extraction value/registration, and added a single Bella-equivalent core on death. Uses native doll impact feedback. Host price UI is hidden; native guest $0 UI and missing monster red-flash support remain documented limitations.

- Added a separately spawned Stalking Doll prototype using native Scream Doll visuals, attacks and synchronization: nearest-living-player targeting, Loom prefab movement speed, silent NavMesh hover pursuit, observation-based freeze and held-object pause. Switched the Debug scenario to the doll. Remote viewport estimation and multiplayer attack interruption still require in-game validation; release spawn progression is unspecified.

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
