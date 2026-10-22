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
Check("Debug contains Animal test scenario", debug.GetType("HellParty.Debugging.DebugAnimalScenario") != null);
Check("Debug Animal scenario forces difficulty five", (int)debug.GetType("HellParty.Debugging.DebugAnimalScenario").Fields.Single(f => f.Name == "ForcedDifficulty").Constant == 5);
Check("Debug difficulty getter uses the test override", debug.GetType("HellParty.DifficultyService").Methods.Single(m => m.Name == "get_Current").Body.Instructions.Any(i => i.OpCode.Code == Code.Ldc_I4_5));
Check("Release excludes debug scenarios and spawn tickets", !release.Types.Any(t => t.Namespace.StartsWith("HellParty.Debugging")));
Check("Release computes difficulty from completed stages", release.GetType("HellParty.DifficultyService").Methods.Single(m => m.Name == "get_Current").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "FromCompletedStages"));
Check("Release includes Animal ability", release.GetType("HellParty.AnimalPoison") != null);
Check("Release includes Animal division and native spawn support", release.GetType("HellParty.AnimalDivision") != null && release.GetType("HellParty.Native.AnimalSpawnTicket") != null);
Check("no custom RPCs required on vanilla guests", !release.Types.SelectMany(t => t.Methods).Any(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "PunRPC")));

if (args.Length > 0)
{
    using var game = ModuleDefinition.ReadModule(args[0]);
    foreach ((string type, string method) in new[] { ("EnemyParent", "Spawn"), ("EnemyParent", "SpawnRPC"), ("EnemyParent", "Despawn"), ("EnemyHealth", "Death"), ("SemiFunc", "EnemySpawn"), ("LevelGenerator", "GenerateDone"), ("PhysGrabObject", "GrabStartedRPC"), ("UraniumScript", "Cloud"), ("PhysGrabObject", "SetPositionRPC"), ("HurtCollider", "EnemyHurt"), ("EnemyHealth", "Hurt"), ("EnemyStateStunned", "Set"), ("EnemyRigidbody", "FixedUpdate") })
        Check($"installed game hook exists: {type}.{method}", game.GetType(type)?.Methods.Any(m => m.Name == method) == true);
    foreach ((string type, string field) in new[] { ("EnemyParent", "Spawned"), ("EnemyParent", "SetupDone"), ("EnemyParent", "valuableSpawnTimer"), ("EnemyHealth", "healthCurrent"), ("EnemyHealth", "dead"), ("EnemyAnimal", "playerTarget"), ("HurtCollider", "enemyLogic"), ("HurtCollider", "physLogic") })
        Check($"installed game native field exists: {type}.{field}", game.GetType(type)?.Fields.Any(f => f.Name == field) == true);
    Check("installed game gas has native all-client destruction callback", game.GetType("PhysGrabObjectImpactDetector").Methods.Single(m => m.Name == "DestroyObjectRPC").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "onDestroy"));
    Check("installed game stun owner field exists for birth readiness", game.GetType("EnemyStateStunned").Fields.Any(f => f.Name == "enemy" && f.FieldType.FullName == "Enemy"));
    Check("native stun owner initializes in Start, requiring birth protection", game.GetType("EnemyStateStunned").Methods.Single(m => m.Name == "Start").Body.Instructions.Any(i => i.OpCode.Code == Code.Stfld && i.Operand is FieldReference f && f.Name == "enemy"));
}

Console.WriteLine($"All {passed} checks passed.");
