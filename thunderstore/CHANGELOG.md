# Changelog

## 0.2.0 — Release candidate

- **Only the host installs; friends join with vanilla R.E.P.O.**
- Add an explicit developer-only Debug build option, `-TestScenario CleanupCrew`, with isolated output, difficulty 5 and one Cleanup Crew plus a shotgun and tranquilizer near the starting player. Default Debug and Release exclude the scenario code; Release rejects test options. Normal monster AI and vanilla guest support are preserved.
- Add Cleanup Crew's Sticky Head at difficulty 1: native explosive heads stick to their target within 1.5m and detach on Grab. Initial attachment consumes no cooldown; detaching or transferring from the previous carrier starts three seconds before another attachment or transfer. Held heads cannot attach. Native explosion timing and final-target hatch inheritance are preserved. Normal builds need no debug setup or guest-side mod.
- Increase Huntsman and Loom's shared Abduction range from 4m to 5m, including the 5m boundary.
- Add natural Elsa's Assassination at difficulty 5. While following a player with no other living player within 20m of the target, Elsa starts its native growth at 5m and attacks. A living teammate entering the target's 20m radius cancels the mode immediately, even during growth.
- Reentry cooldown lasts ten seconds from the end of assassination. First use is immediate and cancellation never waits for a cooldown. There is no separate assassination lifetime; native stun, target loss, death and despawn still apply. Ordinary native aggression is preserved. Summoned Elsas, including Bowtie Brothers/Revenge, are excluded.
- Add boundary/cooldown/origin tests and an Elsa multiplayer playtest guide. No temporary debug spawns or forced difficulty are included.
- Improve Trudge reinforcement placement: search nearby clear, connected floor up to 6m away and retry blocked slots every 0.5 seconds for up to two additional seconds. Successful slots are not duplicated; exhausted placement attempts produce a warning instead of an exception.
- Reuse fully despawned Monster Wave and Cleanup Crew summons within their own per-stage prefab pools after a two-second RPC drain. Reset per-life targets and timers through native spawning instead of allocating new room ViewIDs on every summon. Check complete prefab ViewID capacity before network creation, preserving 32 IDs of native headroom; delay waves or skip Cleanup Crew summons when capacity is insufficient, with throttled warnings.

## 0.1.0 — Release candidate

- **Only the host installs. Friends can join with unmodded R.E.P.O. and experience HellParty's monster abilities and Monster Waves without installing HellParty or BepInEx.**

- Replace the text icon with cartoon fan art of crowned Apex Predator, Clown and Reaper, based on reviewed visual references. Keep the master artwork and prompt in the repository and export a 256x256 package icon.
- Move actual-play test guides into docs/playtests and update links. Rewrite both READMEs for players, describing each monster's final abilities without tier-by-tier balance tables; retain development notes at the end.

- Prepare an original package icon, installation/removal/support documentation and validated metadata. Add strict ZIP/Release DLL verification, negative package tests and a single-command release verification receipt. Roll back own Harmony patches when initialization fails; make Release compiler warnings fail the build.

- Rebalance unlocks: Cleanup Crew Hatching and Bella Suicide at tier 2; Trudge Muster and Huntsman Abduction at tier 1; Mentalist Reflection Shield at tier 3. Existing upgrades and other ability rules remain unchanged.

- Remove temporary monster/weapon spawns, forced difficulty, test keys, solo abduction and summoned Upscream disguise overrides, and lifecycle/navigation diagnostics. Both Debug and Release use actual stage progression. Refresh normal multiplayer documentation and regression checklists.
- Fix Stalking Doll movement using gravity and upright physics, supported steps up to 50cm with verified forward landing, and collision-contact sliding past walls and other dolls. Prevent self-inflicted terrain/passive-contact damage while retaining incoming physical attacks.
- Start difficulty 1 at stage 1, increasing every three completed stages to a maximum of 5 at stage 13. Keep difficulty 0 as the vanilla baseline.
- Add tier-4 Headman Protection: while idle, inherit nearby allies' targets or identified attackers within 20m. Exclude borrowed Reaper bodies.
- Add tier-1 Trudge Muster (소집): every ground slam summons two random native Tick/Gnome/Banger after three seconds, sharing the last target. Summons have no HellParty abilities, timed retirement or post-death respawn; native Banger detonation remains.
- Add Clown tier-3 Laser Staff Summon and tier-5 Laser Shield. Targetless gun hits have damage/stun blocked and trigger a fixed-aim Staff shot after two seconds. Surviving staves become collectible loot on owner death.
- Add tier-4 Loom Abduction using Huntsman's shared rules. Both use 4m range, clear vision rays, at least two living players, one global captive slot, 2 damage per second after three seconds, and a five-second release cooldown. Captives cannot grab their carrier; Huntsman does not shoot or investigate captive sounds while carrying.
- Add Huntsman tier-4 Death Tactics with a pinned, thrown native Human Grenade that players can recover before detonation.
- Add Cleanup Crew Hatching/Hatching2: a targeted head explosion summons a coreless Spewer after two seconds, replaced by Upscream at tier 5. Summons retire after ten seconds; Hatching2 Upscream also retires immediately after its applied throw.
- Add natural Upscream Big-valuable disguises using 24 verified native resource paths, excluding cores, Surplus, Telescope and Unicorn Horn. Ability/wave-summoned Upscream cannot disguise.
- Replace Reaper Blind with tier-2 Sprint: transform into native Headman at 10m target distance and return within 3m, with a ten-second use cooldown, shared health and original Reaper core rewards. Shadow Child retains Blind, creating native player-only gas at its target's saved eye position every three seconds of continuous targeting.
- Update Monster Wave: 20m radius, a three-second posture-based evasion warning, initial Rugrat/Upscream/Headgrab requests at three seconds, and two Gnomes per second from seconds 4–19. Twenty-second total lifetime includes the warning. Use one-second retries, revalidated spawn caches, nearest-player candidate ordering and greater-than-three-metre player clearance. Ignore cores, internal props and in-area destruction; throttle admission checks to one per three seconds. Failed replacements keep the old wave. Break the Gramophone through native effects on combat end and cancel warnings silently.
- Remove Birthday Boy Eyeball generation. Death balloon pops create no extra loot or Gnomes.


- Add the host-only BepInEx/Harmony scaffold, separate build configurations, deployment/launch/package scripts, deterministic rule tests and installed-game method/RPC contract checks.
- Add Apex Predator Clone1/2/3 at tiers 1/3/5 with limits 1/2/3, a shared thirty-second single-clone respawn, coreless radiation-gas clone deaths and a collectible Crown on original death. Clones return at 15m through native despawn/respawn while retaining health. Crown placement uses 70% of its original height offset and a 0.25m backward correction.
- Add Animal tier-2 gas at the current target's position within 3m, on a ten-second cooldown, using native Cleanup Crew-equivalent player-only gas. Tier-3 division creates two half-health children after 0.5 seconds; each awards one core if killed within twenty seconds of actual spawn.
- Add Birthday Boy balloon tiers 1/2/3: proximity pops after three seconds within 3m; Gnomes on auto/physical pops with a living player within 10m; tier-2 auto-pop aggro; and tier-3 nonlethal damage bursts within 10m that share the original's player target.
- Add Bella's tier-2 bell-linked mounted Clown Doll suicide and tier-3 Tiny bait. Bait triggers an attack on every new grab, is replaced on later bells and disappears at 15m from a living owner. Surviving items become collectible loot on death. Mounted doll explosions exclude their own Bella.
- Add Bowtie tier-2 Brothers and tier-4 Revenge. Coreless Elsa inherits a target, plays native transformation and retires with animation after ten seconds or replacement. Revenge spawns after 0.5 seconds at the death position. Own-summoner wind is ignored; no three-hit retirement rule is used.
- Add Chef tier-1 Frog Mimic with 20m unobstructed detection and one-second retry, shared health/durability, five-second visibility-loss reveal, full native attack preparation after grab release, no redisguise during pursuit, and native core rewards for weapon/environmental destruction. Reject drones and Gnome valuable targeting.
- Add Stalking Doll companions, one per Loom/Robe instance from tier 1. Health is 100 and native displayed value follows the health percentage without extraction credit. Unobserved dolls silently stalk the nearest living player at Loom speed. Grab attempts cancel and hurt the grabber for 10; three cumulative attempts trigger rage at triple speed, ignoring observation and maintaining attacks while moving. Tier-3 Rampage also triggers on a valid normal hit. Rage ends on a player death or thirty seconds without a valid hit. Death awards Bella-type cores.
- Add Peeper Gnomes on valid hits from tier 1 and a 30% extra Banger chance from tier 2. Both share the original target and count toward a per-Peeper lifetime total of ten summons.
- Add Mentalist tier-3 Reflection Shield while targetless: block native gun damage/stun, create a native laser gun regardless of the incoming weapon, lock aim immediately and fire after two seconds at native damage.
- Rebind summoned native navigation after spawn to prevent stationary Gnomes. Preserve vanilla network prefabs/RPCs for guests and guard object retirement, late grab requests and invalid healing recipients without hiding RPC warnings.
