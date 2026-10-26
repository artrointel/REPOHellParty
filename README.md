<p align="center">
  <img src="thunderstore/icon.png" alt="HellParty's crowned duck, laser Clown and Reaper" width="320" />
</p>

# HELL PARTY

**Only the host installs. Play HellParty with friends on unmodded R.E.P.O.**

> Your friends need no mods, no BepInEx and no extra setup. They join your lobby with vanilla R.E.P.O. and share the new monster abilities and Monster Waves.

**The duck brought friends. The balloons brought Gnomes. Your team brought a cart.**

HellParty turns a familiar R.E.P.O. loot run into a monster party. Valuables hide ambushes, enemies call for backup, and a clean kill can leave one last surprise. Bring your friends, watch each other's backs, and try to get the loot home between the screams.

[Meet the monsters](#meet-the-party) · [Monster Waves](#you-broke-it-now-the-music-starts) · [Installation](#installation) · [Development](#development-and-contributing)

## A few reasons to watch your back

- A doll waits perfectly still while you look at it, then silently follows when everyone looks away. Make it angry and eye contact stops helping.
- A fight can bring reinforcements, and a defeated monster can leave a dangerous encore.
- Some risks pay off: a duck's crown, Bella's leftovers and a Clown's surviving staff can become loot.

## Meet the party

These are the monsters' **fully upgraded abilities**. Exact values and unlock conditions may change with balance updates.

| Monster | What changes for you |
| --- | --- |
| **Apex Predator** | The duck arrives with up to three doubles. Fake deaths leave radiation gas. Find the crowned original, defeat it to end the family reunion, and collect the crown. |
| **Animal** | Get close and gas appears at its target's feet. Kill it and two half-health Animals take its place. Hunt them before they disappear to claim their cores. |
| **Bella** | Her passenger is a bell-linked explosive Clown Doll. The tiny valuables she drops are bait: grabbing one earns an immediate attack. Surviving items become loot when she dies. |
| **Birthday Boy** | Linger beside a balloon or pop one and a Gnome can burst out. Hurt the boy and nearby balloons pop together, bringing reinforcements that share his target. |
| **Bowtie** | Wind attacks bring an Elsa beside the target, complete with her muscle transformation. Defeating Bowtie can summon a revenge Elsa at its death spot. |
| **Chef** | That Frog collectible might be the monster. Grab it and Chef breaks free to attack; recognize the disguise and you can destroy it for the kill reward. |
| **Upscream** | A large valuable can hide an Upscream. Pick it up and your next trip across the room may not be voluntary. |
| **Cleanup Crew** | Explosive heads stick to you and hop to nearby teammates. Grab one off, but the fuse keeps ticking. When this deadly game of hot potato ends, an Upscream hatches at the blast site and hunts the last target. |
| **Clown** | Laser staves join its attacks. Shoot it before it has a target and it blocks the hit and retaliates with a staff. Move before the fixed aim fires; surviving staves become loot on its death. |
| **Mentalist** | Shooting a Mentalist with no target triggers a laser-gun counter instead of damage or stun. Aim locks immediately, leaving a short window to move out of the shot. |
| **Huntsman** | Get too close during pursuit and it can carry you away. A teammate can free you by hitting or stunning it. Even its death can leave a live Human Grenade. |
| **Loom** | It arrives with a Stalking Doll and can abduct a nearby player. Carriers share one captive limit, giving the rest of the team a chance to organize a rescue. |
| **Robe** | It brings a Stalking Doll of its own. The doll acts independently and follows the nearest living player. |
| **Stalking Doll** | It stalks while unseen. Repeated grab attempts or allowing it to land a hit can trigger rage: faster movement, constant attacks and no respect for eye contact. A player death or a long dry spell without hits calms it down. |
| **Peeper** | Every damaging hit can bring another Gnome, sometimes with a bonus Banger. Rescue your teammate before a nuisance becomes a crowd. |
| **Elsa** | Wander away from your teammates and the friendly follower can transform into an attacker when it closes in. A nearby living teammate immediately ends this assassination mode. Bowtie summons keep their own behavior. |
| **Headman** | A nearby ally's target can become its target too. Attack another monster near an idle Headman and you may find yourself fighting both. |
| **Trudge** | Each ground slam calls two random Tick/Gnome/Banger reinforcements after a short delay. Dodging the hammer is only the first thing to worry about. |
| **Reaper** | Pull far enough ahead and it becomes a Headman to close the gap, then returns to Reaper form for the attack. Keep checking behind you. |
| **Shadow Child** | Keep its attention and gas repeatedly forms at eye level. Move away from each cloud before your escape route disappears behind it. |

**The supporting cast:** Summoned Elsa joins Bowtie's attacks; Gnome, Banger and Tick reinforce several encounters; Spewer can hatch from Cleanup Crew's head; Rugrat and Headgrab join the wave. Their lifetime and core rewards depend on what summoned them. Gambit, Heart Hugger, Hidden and Oogly receive no separate new ability and retain their native behavior.

## You broke it. Now the music starts.

Breaking an eligible valuable can summon a Gramophone. **Your nearby team has three seconds to crouch or crawl into hiding and avoid the wave.** If someone stays upright, enemies start appearing in blind spots.

Rugrat, Upscream and Headgrab join the party, followed by a stream of Gnomes. Survive twenty seconds from the music object's arrival. Grabbing the Gramophone hurts, so call out the warning and choose between hiding together or holding your ground.

## Installation

Install HellParty in the **host's** BepInEx profile. A mod manager installs the declared BepInExPack dependency automatically. Guests do not need HellParty or BepInEx, and no configuration is required.

For a local release candidate, import the ZIP as a local mod. For manual installation, install BepInEx first and place the single `HellParty.dll` in `BepInEx/plugins/HellParty/`. Keep only one copy of the DLL. Close the game before installation, updates or removal, then restart. To uninstall, remove HellParty through your manager or delete its manually installed folder.

## Found a problem?

Report problems through the repository Issues page with the game and HellParty versions, host/guest roles, other installed mods, reproduction steps and relevant `BepInEx/LogOutput.log` lines. Mention whether a problem appears only for vanilla guests.

## Development and contributing

HellParty uses native prefabs, animations and network messages. The build uses R.E.P.O. 0.4.4 compile-time references; automated rule/game-contract checks and in-game multiplayer checks cover different parts of verification. Default Debug and Release builds exclude test scenarios and use normal progression. An explicit Debug-only option enables the separate Cleanup Crew test setup.

Build instructions, design notes, grouped playtest documents and release verification tools are in the repository. The package contains HellParty's DLL, documentation, metadata and its cartoon icon; game, Unity and BepInEx assemblies are not bundled.

- [Development setup and builds](docs/development.md)
- [Playtest guides](docs/playtests/README.md)
- [Monster designs](docs/monsters/README.md) · [Ability planning table](docs/monster-abilities.md)
- [Monster Wave design](docs/events/monster-wave.md)
- [Release readiness and verification](docs/release-readiness.md) · [Icon artwork and references](assets/branding/README.md)

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Dev.ps1 -Offline
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Release.ps1 -Offline
```

For the Cleanup Crew test setup, close the game and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1 -TestScenario CleanupCrew
```

This separate Debug build sets host difficulty to 5 and prepares one Cleanup Crew, one shotgun and one tranquilizer near the starting player. To return to normal play, close the game and run the same script without `-TestScenario CleanupCrew` to rebuild and deploy the default build. See the development guide for VS Code tasks and debugger support.

Automated checks do not replace in-game multiplayer testing. Use the playtest guides to check animations, damage and synchronization with vanilla guests. Detailed design and development documents are currently written in Korean.

R.E.P.O. and its original monster designs belong to semiwork. HellParty is an unofficial fan mod.
