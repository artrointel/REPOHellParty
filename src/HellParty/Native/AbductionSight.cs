using UnityEngine;

namespace HellParty.Native;

internal static class AbductionSight
{
    internal static bool Clear(Enemy enemy, EnemyParent parent, PlayerAvatar player)
    {
        if (enemy.Vision == null || enemy.Vision.VisionTransform == null || player.PlayerVisionTarget == null ||
            player.PlayerVisionTarget.VisionTransform == null) return false;
        Vector3 origin = enemy.Vision.VisionTransform.position;
        Vector3 ray = player.PlayerVisionTarget.VisionTransform.position - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
                     SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(parent.transform) || hit.transform.GetComponentInParent<PlayerAvatar>() != null ||
                hit.transform.GetComponentInParent<PlayerController>() != null || hit.transform.GetComponentInParent<PlayerTumble>() != null) continue;
            return false;
        }
        return true;
    }
}
