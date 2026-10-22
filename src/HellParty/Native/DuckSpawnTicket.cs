using System.Collections;
using UnityEngine;

namespace HellParty.Native;

// Host-local metadata. Native EnemyParent/Enemy RPCs remain untouched on guests.
internal sealed class DuckSpawnTicket : MonoBehaviour
{
    internal Vector3 Position;
    internal bool Pending;
    internal bool HasSpawned;
    internal int? SavedHealth;

    internal void Prepare(Vector3 position, int? health = null)
    {
        Position = position;
        SavedHealth = health;
        Pending = true;
    }

    internal static EnemyParent Create(Vector3 position, DuckFamily owner)
    {
        GameObject root = VanillaCatalog.Spawn(VanillaCatalog.DuckPrefab(), position, Quaternion.identity);
        EnemyParent parent = root.GetComponent<EnemyParent>();
        var ticket = root.AddComponent<DuckSpawnTicket>();
        ticket.Prepare(position);
        var clone = root.AddComponent<DuckClone>();
        clone.Owner = owner;
        NativeEnemy.Duck(parent)!.GetComponent<EnemyHealth>().spawnValuable = false;
        NativeEnemy.SetupDone(parent) = true;
        // Match LevelGenerator.EnemySpawn: the prefab's AI and physical body
        // must be placed before EnemyParent.Setup starts its native lifecycle.
        NativeEnemy.Duck(parent)!.enemy.EnemyTeleported(position);
        // Native Setup registers the enemy and vision IDs, then starts Logic with a despawn.
        ticket.StartCoroutine(ticket.ActivateAfterSetup(parent));
        return parent;
    }

    private IEnumerator ActivateAfterSetup(EnemyParent parent)
    {
        while (!EnemyDirector.instance.enemiesSpawned.Contains(parent)) yield return null;
        yield return null;
        parent.DespawnedTimerSet(0);
    }
}
