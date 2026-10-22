using HellParty.Native;
using UnityEngine;

namespace HellParty;

internal sealed class DuckClone : MonoBehaviour
{
    internal DuckFamily Owner = null!;
    internal bool Killed;
    internal bool Retired;
    internal bool Alive => !Killed && !Retired && NativeEnemy.Duck(GetComponent<EnemyParent>()) is EnemyDuck duck && NativeEnemy.IsAlive(duck);

    internal void Died()
    {
        if (Killed) return;
        Killed = true;
        if (Owner != null) Owner.CloneDied(this);
        StageRuntime.EmitDeathGas(NativeEnemy.Duck(GetComponent<EnemyParent>())!);
    }
}
