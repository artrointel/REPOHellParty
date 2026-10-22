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
foreach (var build in new[] { debug, release })
{
    Check($"{build.FileName}: no temporary duck test code", !build.Types.Any(t => t.FullName.Contains("DebugScenario") || t.Namespace.StartsWith("HellParty.Debugging")));
    var difficulty = build.GetType("HellParty.DifficultyService").Methods.Single(m => m.Name == "get_Current");
    Check($"{build.FileName}: computes difficulty from completed stages", difficulty.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "FromCompletedStages"));
}
Check("no custom RPCs required on vanilla guests", !release.Types.SelectMany(t => t.Methods).Any(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "PunRPC")));

if (args.Length > 0)
{
    using var game = ModuleDefinition.ReadModule(args[0]);
    foreach ((string type, string method) in new[] { ("EnemyParent", "Spawn"), ("EnemyParent", "SpawnRPC"), ("EnemyParent", "Despawn"), ("EnemyHealth", "Death"), ("SemiFunc", "EnemySpawn"), ("LevelGenerator", "GenerateDone"), ("PhysGrabObject", "GrabStartedRPC") })
        Check($"installed game hook exists: {type}.{method}", game.GetType(type)?.Methods.Any(m => m.Name == method) == true);
    foreach ((string type, string field) in new[] { ("EnemyParent", "Spawned"), ("EnemyParent", "SetupDone"), ("EnemyHealth", "healthCurrent"), ("EnemyHealth", "dead") })
        Check($"installed game native field exists: {type}.{field}", game.GetType(type)?.Fields.Any(f => f.Name == field) == true);
    Check("installed game gas has native all-client destruction callback", game.GetType("PhysGrabObjectImpactDetector").Methods.Single(m => m.Name == "DestroyObjectRPC").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "onDestroy"));
}

Console.WriteLine($"All {passed} checks passed.");
