using HellParty.Rules;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Standalone deterministic unit/assembly-contract runner. Does not load Unity
// or run the game, and fails the process when any assertion fails.
int passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

foreach ((int completed, int expected) in new[] { (-1, 0), (0, 0), (2, 0), (3, 1), (5, 1), (6, 2), (8, 2), (9, 3), (11, 3), (12, 4), (14, 4), (15, 5), (99, 5), (int.MaxValue, 5) })
    Check($"completed stages {completed} -> difficulty {expected}", DifficultyRules.FromCompletedStages(completed) == expected);
Check("stage starts at one", DifficultyRules.StageNumber(0) == 1);
Check("stage seven after six completions", DifficultyRules.StageNumber(6) == 7);
Check("stage number does not overflow", DifficultyRules.StageNumber(int.MaxValue) == int.MaxValue);
foreach ((int level, int limit) in new[] { (0, 0), (1, 1), (2, 1), (3, 2), (4, 2), (5, 3) })
    Check($"difficulty {level} -> {limit} clones", DifficultyRules.CloneLimit(level) == limit);
Check("recall at exactly fifteen metres", DifficultyRules.ShouldRecall(225));
Check("no recall below fifteen metres", !DifficultyRules.ShouldRecall(224.99f));
Check("recall above fifteen metres", DifficultyRules.ShouldRecall(226));
Check("old ten-metre threshold no longer recalls", !DifficultyRules.ShouldRecall(100));

foreach ((int level, int seconds) in new[] { (0, 0), (1, 0), (2, 10), (3, 10), (4, 10), (5, 10) })
    Check($"Animal difficulty {level} -> cooldown {seconds}", DifficultyRules.AnimalGasCooldown(level) == seconds);
var gasClock = new TargetSkillClock();
Check("Animal cannot emit before recognition", !gasClock.IsReady(0, false));
Check("Animal first qualifying approach is immediately ready", gasClock.IsReady(100, true));
gasClock.Used(100, 10);
Check("Animal gas waits ten seconds", !gasClock.IsReady(109.999, true) && gasClock.IsReady(110, true));
Check("Animal expired cooldown still requires target", !gasClock.IsReady(200, false));
Check("Animal reacquisition does not restart expired cooldown", gasClock.IsReady(200, true));
gasClock.Used(200, 10);
Check("Animal target loss does not bypass active cooldown", !gasClock.IsReady(205, false) && !gasClock.IsReady(206, true));
gasClock.Used(500, 10);
Check("Animal long frame emits once without catch-up burst", !gasClock.IsReady(500, true) && gasClock.IsReady(510, true));
Check("Animal gas includes exactly three metres", DifficultyRules.AnimalGasInRange(9));
Check("Animal gas works below three metres", DifficultyRules.AnimalGasInRange(8.99f));
Check("Animal gas excludes just over three metres", !DifficultyRules.AnimalGasInRange(9.01f));
foreach (int level in new[] { 0, 1, 2, 3, 4, 5 })
    Check($"Animal division unlock at difficulty {level}", DifficultyRules.AnimalDivisionUnlocked(level) == (level >= 3));
Check("Animal child health is half native health", DifficultyRules.AnimalOffspringHealth(200) == 100);
Check("Animal child odd health rounds down", DifficultyRules.AnimalOffspringHealth(101) == 50);
Check("Animal child health stays positive", DifficultyRules.AnimalOffspringHealth(1) == 1);
var childLife = new AnimalOffspringLife();
Check("Animal child lifetime waits for actual spawn", !childLife.Expire(1000));
Check("Animal pre-spawn death cannot award an orb", !childLife.Die(50));
childLife.Start(100);
Check("Animal child stays alive until twenty seconds", !childLife.Expire(119.999));
childLife.Start(119);
Check("Animal child expires at exactly twenty seconds", childLife.Expire(120) && childLife.Expired);
Check("Animal expiry cannot drop an orb", !childLife.Die(120));
Check("Animal expiry is handled only once", !childLife.Expire(121));
var killedChild = new AnimalOffspringLife();
killedChild.Start(100);
Check("Animal child kill before expiry grants an orb", killedChild.Die(119.999));
Check("Animal death cannot award twice", !killedChild.Die(119.999));
Check("Animal dying just before expiry keeps its reward through death animation", !killedChild.Expire(121) && !killedChild.Expired);
var boundaryChild = new AnimalOffspringLife();
boundaryChild.Start(100);
Check("Animal lethal hit at expiry gets no orb even before Update", !boundaryChild.Die(120) && boundaryChild.Expired);
var oneOfTwo = new[] { new AnimalOffspringLife(), new AnimalOffspringLife() };
foreach (var life in oneOfTwo) life.Start(0);
Check("Animal killing one child rewards independently of sibling expiry", oneOfTwo[0].Die(10) && oneOfTwo[1].Expire(20));

var clock = new RespawnClock();
foreach (int level in new[] { 0, 1, 2, 3, 4, 5 })
    Check($"Bowtie Brothers unlock at difficulty {level}", DifficultyRules.BowtieBrothersUnlocked(level) == (level >= 2));
var brotherLife = new BowtieBrotherLife();
foreach (int level in new[] { 0, 1, 2, 3, 4, 5 })
    Check($"Bowtie Revenge unlock at difficulty {level}", DifficultyRules.BowtieRevengeUnlocked(level) == (level >= 4));
var revenge = new BowtieRevengeTrigger();
Check("Bowtie death below tier four cannot queue revenge", !revenge.TryQueue(3));
Check("Bowtie qualifying death queues revenge once", revenge.TryQueue(4));
Check("Bowtie duplicate death cannot create another revenge summon", !revenge.TryQueue(4) && !revenge.TryQueue(5));
revenge.NewLife();
Check("Bowtie new life restores revenge trigger", revenge.TryQueue(5));
var anotherRevenge = new BowtieRevengeTrigger();
Check("Bowtie revenge triggers are isolated per owner", anotherRevenge.TryQueue(4));
Check("Bowtie brother lifetime does not run during setup", !brotherLife.Expire(1000));
brotherLife.Start(100);
Check("Bowtie brother lives until ten seconds from actual spawn", !brotherLife.Expire(109.999));
brotherLife.Start(109);
Check("Bowtie brother repeated spawn notification cannot extend lifetime", brotherLife.Expire(110));
Check("Bowtie brother retirement is permanent", brotherLife.Retired && !brotherLife.Expire(111));
var replacedBrother = new BowtieBrotherLife();
replacedBrother.Start(0);
replacedBrother.Retire();
Check("Bowtie replacement ends previous brother before expiry", replacedBrother.Retired && !replacedBrother.Expire(5));
var cancelledBrother = new BowtieBrotherLife();
cancelledBrother.Retire();
cancelledBrother.Start(100);
Check("Bowtie pending replacement cannot activate later", cancelledBrother.Retired && !cancelledBrother.Started);
var independentBrother = new BowtieBrotherLife();
independentBrother.Start(105);
Check("Bowtie new brother has an independent ten-second lifetime", !independentBrother.Expire(110) && independentBrother.Expire(115));
var wind = new BowtieWindCycle();
Check("Bowtie wind without target cannot summon", !wind.TrySummon(false));
Check("Bowtie first valid wind start summons once", wind.TrySummon(true));
wind.Observe(true);
Check("Bowtie repeated wind animation event cannot duplicate summon", !wind.TrySummon(true));
wind.Observe(false);
Check("Bowtie new wind attack can replace the previous brother", wind.TrySummon(true));
foreach (int level in new[] { 0, 1, 2, 3, 4, 5 })
{
    Check($"Bella suicide unlock at difficulty {level}", DifficultyRules.BellaSuicideUnlocked(level) == (level >= 1));
    Check($"Bella bait unlock at difficulty {level}", DifficultyRules.BellaBaitUnlocked(level) == (level >= 3));
}
var dollSequence = new BellaDollSequence();
Check("Bella bait stays below fifteen metres", !DifficultyRules.BellaBaitOutOfRange(224.99f));
Check("Bella bait expires at exactly fifteen metres", DifficultyRules.BellaBaitOutOfRange(225f));
Check("Bella bait expires beyond fifteen metres", DifficultyRules.BellaBaitOutOfRange(225.01f));
Check("Bella adjacent bait remains collectible on nearby owner death", !DifficultyRules.BellaBaitOutOfRange(0));
Check("Bella doll does not warn before a native bell", !dollSequence.TryAdvance(true));
dollSequence.ObserveWarning(1);
Check("Bella first warning touches the native nose once", dollSequence.TryAdvance(true) && !dollSequence.TryAdvance(true));
dollSequence.ObserveWarning(2);
Check("Bella warning waits for native clown animation", !dollSequence.TryAdvance(false));
Check("Bella second warning resumes after animation", dollSequence.TryAdvance(true));
dollSequence.ObserveWarning(2);
Check("Bella extra bell pulses do not advance doll warning", !dollSequence.TryAdvance(true));
dollSequence.ObserveWarning(1);
Check("Bella interrupted warning sequence does not rewind doll", !dollSequence.TryAdvance(true));
dollSequence.ObserveWarning(3);
Check("Bella third warning triggers native countdown once", dollSequence.TryAdvance(true) && !dollSequence.TryAdvance(true));
var skippedWarning = new BellaDollSequence();
skippedWarning.ObserveWarning(3);
Check("Bella skipped warning stages queue all native touches", skippedWarning.TryAdvance(true) && !skippedWarning.TryAdvance(false) && skippedWarning.TryAdvance(true) && skippedWarning.TryAdvance(true) && !skippedWarning.TryAdvance(true));
var releasedDoll = new BellaDollSequence();
releasedDoll.ObserveWarning(2);
releasedDoll.TryAdvance(true);
releasedDoll.Stop();
Check("Bella death cancels pending doll touches", !releasedDoll.TryAdvance(true));
releasedDoll.ObserveWarning(3);
Check("released doll cannot be rearmed by a late bell", !releasedDoll.TryAdvance(true));
foreach ((int level, int tier) in new[] { (0, 0), (1, 1), (2, 2), (3, 3), (4, 3), (5, 3) })
    Check($"Birthday Boy difficulty {level} -> balloon tier {tier}", DifficultyRules.BirthdayBalloonTier(level) == tier);
var proximity = new BalloonProximityClock();
Check("balloon proximity starts without owner recognition", !proximity.Observe(100, true));
Check("balloon does not pop before three seconds", !proximity.Observe(102.999, true));
Check("balloon pops at exactly three seconds", proximity.Observe(103, true));
Check("balloon proximity triggers once", !proximity.Observe(110, true));
var interruptedProximity = new BalloonProximityClock();
interruptedProximity.Observe(0, true);
interruptedProximity.Observe(2.9, false);
Check("leaving balloon radius resets continuous exposure", !interruptedProximity.Observe(3, true) && !interruptedProximity.Observe(5.999, true));
Check("fresh continuous exposure triggers after three seconds", interruptedProximity.Observe(6, true));
var rewards = new BirthdayBalloonRewards<int>();
rewards.Register(1);
Check("tier one automatic pop creates Gnome with nearby player", rewards.Remove(1, BalloonPopCause.Automatic, true, 1) == BalloonReward.Gnome);
Check("same balloon cannot reward twice", rewards.Remove(1, BalloonPopCause.Automatic, true, 2) == BalloonReward.None);
rewards.Register(2);
Check("tier one player pop creates Gnome", rewards.Remove(2, BalloonPopCause.Player, true, 1) == BalloonReward.Gnome);
rewards.Register(3);
Check("tier two player pop creates Gnome", rewards.Remove(3, BalloonPopCause.Player, true, 2) == BalloonReward.Gnome);
rewards.Register(4);
Check("manual replacement does not create Gnome", rewards.Remove(4, BalloonPopCause.Other, true, 2) == BalloonReward.None);
rewards.Register(5);
Check("tier zero automatic pop cannot create reward", rewards.Remove(5, BalloonPopCause.Automatic, true, 0) == BalloonReward.None);
var deathRewards = new BirthdayBalloonRewards<int>();
foreach (int key in new[] { 1, 2, 3, 4, 5 }) deathRewards.Register(key);
deathRewards.BeginDeath();
deathRewards.BeginDeath();
Check("older balloons do not award death loot", deathRewards.Remove(1, BalloonPopCause.Other, false, 2) == BalloonReward.None && deathRewards.Remove(2, BalloonPopCause.Other, false, 2) == BalloonReward.None);
foreach (int key in new[] { 3, 4, 5 })
    Check($"recent balloon {key} awards Eyeball independent of recognition", deathRewards.Remove(key, BalloonPopCause.Automatic, false, 2) == BalloonReward.Eyeball);
Check("death cleanup cannot duplicate an Eyeball", deathRewards.Remove(5, BalloonPopCause.Player, true, 2) == BalloonReward.None);
var sparseDeath = new BirthdayBalloonRewards<int>();
sparseDeath.Register(10);
sparseDeath.BeginDeath();
Check("death with one balloon awards only one Eyeball", sparseDeath.Remove(10, BalloonPopCause.Other, false, 2) == BalloonReward.Eyeball);
var basicDeath = new BirthdayBalloonRewards<int>();
basicDeath.Register(1);
basicDeath.BeginDeath();
Check("tier one death cleanup creates neither Gnome nor Eyeball", basicDeath.Remove(1, BalloonPopCause.Player, true, 1) == BalloonReward.None);
var otherOwner = new BirthdayBalloonRewards<int>();
otherOwner.Register(1);
Check("balloon identities are isolated by owner", otherOwner.Remove(1, BalloonPopCause.Automatic, true, 1) == BalloonReward.Gnome);
var reusedPosition = new BirthdayBalloonRewards<int>();
foreach (int key in new[] { 1, 2, 3, 4, 1 }) reusedPosition.Register(key);
reusedPosition.BeginDeath();
Check("reused placement position counts as newest generation", reusedPosition.Remove(1, BalloonPopCause.Other, false, 2) == BalloonReward.Eyeball && reusedPosition.Remove(2, BalloonPopCause.Other, false, 2) == BalloonReward.None);
reusedPosition.Remove(3, BalloonPopCause.Other, false, 2);
reusedPosition.Remove(4, BalloonPopCause.Other, false, 2);
reusedPosition.NewLife();
reusedPosition.Register(1);
Check("new monster life restores normal automatic pop reward", reusedPosition.Remove(1, BalloonPopCause.Automatic, true, 1) == BalloonReward.Gnome);
var quietTierTwo = new BirthdayBalloonRewards<int>();
quietTierTwo.Register(1);
Check("tier two automatic pop uses nearby player eligibility", quietTierTwo.Remove(1, BalloonPopCause.Automatic, true, 2) == BalloonReward.Gnome);
quietTierTwo.Register(2);
Check("distant direct pop creates no Gnome", quietTierTwo.Remove(2, BalloonPopCause.Player, false, 2) == BalloonReward.None);
foreach (BalloonPopCause cause in new[] { BalloonPopCause.Player, BalloonPopCause.Physical, BalloonPopCause.Automatic })
    foreach (bool nearby in new[] { false, true })
    {
        var physical = new BirthdayBalloonRewards<int>();
        physical.Register(1);
        Check($"tier one {cause} nearby={nearby}", physical.Remove(1, cause, nearby, 1) == (nearby ? BalloonReward.Gnome : BalloonReward.None));
    }
Check("balloon radius includes exactly ten metres", DifficultyRules.BirthdayBalloonInRange(100));
Check("balloon radius includes less than ten metres", DifficultyRules.BirthdayBalloonInRange(99.99f));
Check("balloon radius excludes beyond ten metres", !DifficultyRules.BirthdayBalloonInRange(100.01f));
Check("tier three reacts to actual nonlethal HP loss", DifficultyRules.BirthdayDamageTriggers(100, 90, 3));
Check("lower tiers do not react to body damage", !DifficultyRules.BirthdayDamageTriggers(100, 90, 2));
Check("blocked or rounded-zero damage does not trigger burst", !DifficultyRules.BirthdayDamageTriggers(100, 100, 3));
Check("healing does not trigger burst", !DifficultyRules.BirthdayDamageTriggers(90, 100, 3));
Check("lethal damage leaves death rewards in control", !DifficultyRules.BirthdayDamageTriggers(100, 0, 3));
foreach (int tier in new[] { 0, 1, 2, 3 })
{
    var damage = new BirthdayBalloonRewards<int>();
    damage.Register(1);
    Check($"damage burst tier {tier} independent of nearby player", damage.Remove(1, BalloonPopCause.Damage, false, tier) == (tier >= 3 ? BalloonReward.Gnome : BalloonReward.None));
    Check($"damage burst cannot reward twice at tier {tier}", damage.Remove(1, BalloonPopCause.Damage, true, tier) == BalloonReward.None);
}
var fatalBurst = new BirthdayBalloonRewards<int>();
fatalBurst.Register(1);
fatalBurst.BeginDeath();
Check("death reward overrides a concurrent damage burst", fatalBurst.Remove(1, BalloonPopCause.Damage, true, 3) == BalloonReward.Eyeball);
var remainingDeath = new BirthdayBalloonRewards<int>();
foreach (int key in new[] { 1, 2, 3, 4, 5 }) remainingDeath.Register(key);
remainingDeath.Remove(5, BalloonPopCause.Automatic, false, 1);
remainingDeath.BeginDeath();
Check("already popped newest balloon is excluded from death rewards", remainingDeath.Remove(5, BalloonPopCause.Other, false, 2) == BalloonReward.None);
Check("death rewards select newest remaining balloon", remainingDeath.Remove(2, BalloonPopCause.Other, false, 2) == BalloonReward.Eyeball);
remainingDeath.BeginDeath();
Check("repeated death notifications cannot refill reward slots with older balloons", remainingDeath.Remove(1, BalloonPopCause.Other, false, 2) == BalloonReward.None);
Check("no respawn without a loss", !clock.IsReady(1000));
clock.NoteLoss(100);
Check("no respawn before 30 seconds", !clock.IsReady(129.999));
clock.NoteLoss(110);
clock.NoteLoss(120);
Check("additional losses do not reset shared cooldown", clock.IsReady(130));
clock.Spawned(130, stillMissing: true);
Check("three simultaneous losses do not yield three respawns at 30s", !clock.IsReady(130));
Check("second replacement waits another 30 seconds", !clock.IsReady(159.999) && clock.IsReady(160));
clock.Spawned(160, stillMissing: true);
Check("third replacement at 90 seconds", !clock.IsReady(189.999) && clock.IsReady(190));
clock.Spawned(190, stillMissing: false);
Check("full population stops the respawn clock", !clock.IsReady(1000));
clock.NoteLoss(200);
clock.Spawned(500, stillMissing: true);
Check("long frame does not cause a catch-up spawn burst", !clock.IsReady(500) && clock.IsReady(530));
clock.RetryLater(530);
Check("failed safe spawn retries without consuming a 30-second cycle", !clock.IsReady(530) && clock.IsReady(531));
clock.Reset();
Check("original death or new life clears pending respawns", !clock.IsReady(10000));

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
using var debug = ModuleDefinition.ReadModule(Path.Combine(root, "src/HellParty/bin/Debug/netstandard2.1/HellParty.dll"));
using var release = ModuleDefinition.ReadModule(Path.Combine(root, "src/HellParty/bin/Release/netstandard2.1/HellParty.dll"));
Check("Debug contains Bowtie test scenario", debug.GetType("HellParty.Debugging.DebugBowtieScenario") != null);
Check("Debug Bowtie scenario forces difficulty five", (int)debug.GetType("HellParty.Debugging.DebugBowtieScenario").Fields.Single(f => f.Name == "ForcedDifficulty").Constant == 5);
Check("Debug no longer contains Animal test scenario", debug.GetType("HellParty.Debugging.DebugAnimalScenario") == null);
Check("Debug difficulty getter uses the test override", debug.GetType("HellParty.DifficultyService").Methods.Single(m => m.Name == "get_Current").Body.Instructions.Any(i => i.OpCode.Code == Code.Ldc_I4_5));
Check("Release excludes debug scenarios and spawn tickets", !release.Types.Any(t => t.Namespace.StartsWith("HellParty.Debugging")));
Check("Release computes difficulty from completed stages", release.GetType("HellParty.DifficultyService").Methods.Single(m => m.Name == "get_Current").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "FromCompletedStages"));
Check("Release includes Animal ability", release.GetType("HellParty.AnimalPoison") != null);
Check("Release includes Bella abilities and pinned doll", release.GetType("HellParty.BellaAbilities") != null && release.GetType("HellParty.BellaDollCarrier") != null);
Check("Release includes Bowtie Brothers and summoned Elsa lifecycle", release.GetType("HellParty.BowtieBrothers") != null && release.GetType("HellParty.BowtieBrother") != null);
Check("Debug no longer contains Bella scenario", debug.GetType("HellParty.Debugging.DebugBellaScenario") == null);
Check("Release includes Birthday Boy ability", release.GetType("HellParty.BirthdayBoyAbilities") != null);
Check("Release includes Animal division and native spawn support", release.GetType("HellParty.AnimalDivision") != null && release.GetType("HellParty.Native.AnimalSpawnTicket") != null);
Check("no custom RPCs required on vanilla guests", !release.Types.SelectMany(t => t.Methods).Any(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "PunRPC")));

if (args.Length > 0)
{
    using var game = ModuleDefinition.ReadModule(args[0]);
    foreach ((string type, string method) in new[] { ("EnemyParent", "Spawn"), ("EnemyParent", "SpawnRPC"), ("EnemyParent", "Despawn"), ("EnemyHealth", "Death"), ("SemiFunc", "EnemySpawn"), ("LevelGenerator", "GenerateDone"), ("PhysGrabObject", "GrabStartedRPC"), ("UraniumScript", "Cloud"), ("PhysGrabObject", "SetPositionRPC"), ("HurtCollider", "EnemyHurt"), ("EnemyHealth", "Hurt"), ("EnemyStateStunned", "Set"), ("EnemyRigidbody", "FixedUpdate"), ("EnemyBirthdayBoy", "SpawnBalloonRPC"), ("EnemyBirthdayBoy", "RemoveBalloonRPC"), ("EnemyBirthdayBoy", "PopAllBalloonsRPC"), ("EnemyBirthdayBoy", "RemoveBalloon"), ("EnemyBirthdayBoy", "UpdatePlayerTarget"), ("EnemyBirthdayBoy", "OnSpawn"), ("EnemyGnomeDirector", "SetupSingle"), ("EnemyGnomeDirector", "SetTarget"), ("EnemyTricycle", "DoBellRing"), ("EnemyTricycle", "SetState"), ("EnemyTricycle", "OnSpawn"), ("ClownTrap", "TouchNose"), ("PhysGrabObject", "GrabPlayerAddRPC"), ("PhysGrabObject", "GrabStarted"), ("EnemyBowtieAnim", "YellStart"), ("EnemyElsa", "Update"), ("EnemyElsa", "UpdateState"), ("EnemyElsa", "StateTransformBigToSmall"), ("EnemyElsa", "StateDespawnSmall"), ("EnemyElsaAnim", "Despawn"), ("PhysGrabObjectImpactDetector", "OnCollisionStay"), ("EnemyBowtie", "OnSpawn") })
        Check($"installed game hook exists: {type}.{method}", game.GetType(type)?.Methods.Any(m => m.Name == method) == true);
    foreach ((string type, string field) in new[] { ("EnemyParent", "Spawned"), ("EnemyParent", "SetupDone"), ("EnemyParent", "valuableSpawnTimer"), ("EnemyHealth", "healthCurrent"), ("EnemyHealth", "dead"), ("EnemyAnimal", "playerTarget"), ("HurtCollider", "enemyLogic"), ("HurtCollider", "physLogic"), ("EnemyTricycle", "playerTarget"), ("EnemyTricycle", "attackTimer"), ("EnemyTricycle", "attackTimerMax"), ("EnemyTricycle", "lastSeenPlayerTimer"), ("LevelValuables", "tiny"), ("Trap", "trapTriggered"), ("EnemyBowtie", "playerTarget"), ("EnemyElsa", "playerTarget"), ("HurtCollider", "playerCausingHurt"), ("HurtCollider", "playerCausingHurtOverride"), ("PhysGrabObject", "lastPlayerGrabbing"), ("PhysGrabObject", "grabbedTimer") })
        Check($"installed game native field exists: {type}.{field}", game.GetType(type)?.Fields.Any(f => f.Name == field) == true);
    Check("installed game gas has native all-client destruction callback", game.GetType("PhysGrabObjectImpactDetector").Methods.Single(m => m.Name == "DestroyObjectRPC").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "onDestroy"));
    Check("installed game stun owner field exists for birth readiness", game.GetType("EnemyStateStunned").Fields.Any(f => f.Name == "enemy" && f.FieldType.FullName == "Enemy"));
    Check("native stun owner initializes in Start, requiring birth protection", game.GetType("EnemyStateStunned").Methods.Single(m => m.Name == "Start").Body.Instructions.Any(i => i.OpCode.Code == Code.Stfld && i.Operand is FieldReference f && f.Name == "enemy"));
    Check("native Gnome group has player target for shared Birthday Boy aggro", game.GetType("EnemyGnomeDirector").Fields.Any(f => f.Name == "playerTarget" && f.FieldType.FullName == "PlayerAvatar"));
    Check("native Gnome group supports player attack state transition", game.GetType("EnemyGnomeDirector").Methods.Any(m => m.Name == "UpdateState" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Resolve().Fields.Any(f => f.Name == "AttackPlayer")));
    Check("native navigation supports forced Warp for off-mesh births", game.GetType("EnemyNavMeshAgent").Methods.Any(m => m.Name == "Warp" && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "UnityEngine.Vector3" && m.Parameters[1].ParameterType.FullName == "System.Boolean"));
    Check("native navigation supports enabling after spawn activation", game.GetType("EnemyNavMeshAgent").Methods.Any(m => m.Name == "Enable" && m.Parameters.Count == 0));
    Check("native explosion Spawn returns the damage collider carrier", game.GetType("ParticleScriptExplosion").Methods.Any(m => m.Name == "Spawn" && m.ReturnType.FullName == "ParticlePrefabExplosion"));
    Check("native explosion exposes its HurtCollider", game.GetType("ParticlePrefabExplosion").Fields.Any(f => f.Name == "HurtCollider" && f.FieldType.FullName == "HurtCollider"));
    Check("native HurtCollider supports exact enemy self-hit exclusion", game.GetType("HurtCollider").Fields.Any(f => f.Name == "enemyHost" && f.FieldType.FullName == "Enemy"));
    var enemyHurtInstructions = game.GetType("HurtCollider").Methods.Single(m => m.Name == "EnemyHurt").Body.Instructions;
    Check("native enemy wind-hit entry controls physical knockback", enemyHurtInstructions.Any(i => i.Operand is MethodReference m && m.Name == "PhysObjectHurt"));
    Check("native enemy wind-hit entry controls enemy stun", enemyHurtInstructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "EnemyStateStunned" && m.Name == "Set"));
    int ownerCheck = enemyHurtInstructions.ToList().FindIndex(i => i.Operand is FieldReference f && f.Name == "enemyHost");
    Check("native owner exclusion returns before damage and stun", ownerCheck >= 0 && enemyHurtInstructions.Skip(ownerCheck).Take(6).Any(i => i.OpCode.Code == Code.Ret) &&
        enemyHurtInstructions.Take(ownerCheck + 6).All(i => i.Operand is not MethodReference m || (m.Name != "Hurt" && m.Name != "Set")));
    foreach ((string type, string rpc) in new[] { ("ClownTrap", "TouchNoseRPC"), ("EnemyTricycle", "SetStateRPC"), ("PhysGrabObject", "GrabPlayerAddRPC") })
        Check($"Bella native RPC is master-only: {type}.{rpc}", game.GetType(type).Methods.Single(m => m.Name == rpc).Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "MasterOnlyRPC"));
    Check("Bella bait grab request authenticates grabbing player", game.GetType("PhysGrabObject").Methods.Single(m => m.Name == "GrabStartedRPC").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "OwnerOnlyRPC"));
    foreach (string rpc in new[] { "UpdateStateRPC", "UpdatePlayerTargetRPC", "TransformSmallToBigRPC", "TransformBigToSmallRPC" })
        Check($"native Elsa RPC is master-only: {rpc}", game.GetType("EnemyElsa").Methods.Single(m => m.Name == rpc).Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "MasterOnlyRPC"));
    Check("native Elsa growth uses a 4.1-second transformation", game.GetType("EnemyElsa").Methods.Single(m => m.Name == "StateTransformSmallToBig").Body.Instructions.Any(i => i.OpCode.Code == Code.Ldc_R4 && i.Operand is float f && Math.Abs(f - 4.1f) < .001f));
    Check("native Elsa despawn is completed by animation event", game.GetType("EnemyElsaAnim").Methods.Single(m => m.Name == "Despawn").Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "EnemyParent" && m.Name == "Despawn"));
    foreach (string rpc in new[] { "SpawnBalloonRPC", "RemoveBalloonRPC", "PopAllBalloonsRPC" })
        Check($"native balloon RPC is master-only: {rpc}", game.GetType("EnemyBirthdayBoy").Methods.Single(m => m.Name == rpc).Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "MasterOnlyRPC"));
}

Console.WriteLine($"All {passed} checks passed.");
