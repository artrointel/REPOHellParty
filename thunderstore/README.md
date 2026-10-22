# HellParty

Host-only difficulty mod for R.E.P.O. Adds independent difficulty levels 0–5 every three completed stages and Apex Predator clones with Crown identification and a Crown loot reward. Clones drop native radiation gas instead of orbs; lost clones are replaced one at a time on a shared 30-second cooldown.

Animal gains player-only native Cleanup Crew gas at difficulty 2: its current target must be within 3m, with a 10-second cooldown. Gas appears at that player's position when cast and stays there. From difficulty 3 (stage 10), two half-health Animals appear 0.5 seconds after its death instead of an orb. Each child drops one native orb if killed before its 20-second expiry, counted from its actual spawn, and cannot divide again.

From difficulty 1, Birthday Boy balloons automatically pop after a player stays within 3m for 3 seconds. Automatic, player and physical-impact pops spawn one Gnome when a living player is within 10m at the moment of popping. Difficulty 2 adds automatic-pop aggro and up to three Eyeballs from the newest remaining balloons on death. Difficulty 3 adds a burst of all balloons within 10m of the body on actual nonlethal damage, with one Gnome per balloon. Lethal hits retain the death reward rules.

Bella gains a seat-mounted Clown Doll at difficulty 1, linked to native bell warning stages. Difficulty 3 adds random Tiny bait on every bell pulse: the previous bait is silently replaced, every new grab of the current bait immediately starts Bella's native attack against that grabber. Bait disappears at 15m from a living Bella; killing her before it leaves that radius preserves it as collectible loot. Surviving dolls also remain collectible after her death.

Only vanilla network objects and RPCs are used. Actual multiplayer compatibility still needs in-game playtesting. Release uses normal stage progression. The development Debug build forces difficulty 5 and spawns a Bella and test weapons. Other planned monster abilities are not implemented yet.

The package icon is a temporary development asset reused from REPOPocketCartTurret and must be replaced before publication.
