using HellParty.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Native;

// Host-side step assistance. Keep native collider geometry and gravity.
internal static class StalkingDollStep
{
    internal static bool GroundPosition(Rigidbody body, Collider[] colliders, out Vector3 ground)
    {
        ground = default;
        if (!BodyBounds(body, colliders, out Bounds bounds) || !Support(body, bounds, out RaycastHit hit)) return false;
        ground = hit.point;
        return true;
    }

    internal static bool TryClimb(Rigidbody body, Collider[] colliders, Vector3 movement)
    {
        if (body.isKinematic || body.velocity.y > .5f || movement.sqrMagnitude < .000001f) return false;
        if (!BodyBounds(body, colliders, out Bounds bounds)) return false;
        Vector3 foot = new(bounds.center.x, bounds.min.y, bounds.center.z);
        if (!Support(body, bounds, out RaycastHit support)) return false;
        Vector3 forward = movement.normalized;
        float reach = Mathf.Abs(forward.x) * bounds.extents.x + Mathf.Abs(forward.z) * bounds.extents.z +
            Mathf.Clamp(movement.magnitude, .04f, .08f);
        if (!Nearest(foot + Vector3.up * .005f, forward, reach, body, out RaycastHit obstacle) || obstacle.normal.y > .7f)
            return false;
        if (!Terrain(obstacle, body)) return false;
        // Move the body's centre beyond the riser, not merely its toes up to
        // the lip. Validate each candidate's real support and swept body path.
        Vector3 extents = new(Mathf.Max(.01f, bounds.extents.x - .01f), Mathf.Max(.01f, bounds.extents.y - .01f),
            Mathf.Max(.01f, bounds.extents.z - .01f));
        int obstacles = LayerMask.GetMask("Default", "PhysGrabObject", "Enemy", "Player");
        for (int probe = 0; probe < 3; probe++)
        {
            float advance = obstacle.distance + StalkingDollStepRules.Skin * 2 + probe * .06f;
            if (!StalkingDollStepRules.CanAdvance(advance)) break;
            Vector3 horizontal = forward * advance;
            Vector3 above = foot + horizontal;
            above.y = support.point.y + StalkingDollStepRules.MaximumHeight + .05f;
            if (!Nearest(above, Vector3.down, StalkingDollStepRules.MaximumHeight + .1f, body, out RaycastHit top) ||
                !Terrain(top, body) || top.normal.y < .7f) continue;
            float rise = top.point.y - support.point.y;
            if (!StalkingDollStepRules.CanStep(rise, true, true, true)) continue;
            bool walkable = NavMesh.SamplePosition(top.point, out NavMeshHit mesh,
                    StalkingDollStepRules.MaximumHeight + .1f, NavMesh.AllAreas) &&
                Mathf.Abs(mesh.position.y - top.point.y) <= StalkingDollStepRules.MaximumHeight + StalkingDollStepRules.Skin;
            if (!walkable) continue;
            float liftHeight = top.point.y + StalkingDollStepRules.Skin - bounds.min.y;
            if (liftHeight <= .001f) continue;
            Vector3 lift = Vector3.up * liftHeight;
            Collider? upward = Sweep(bounds.center, extents, Vector3.up, liftHeight, obstacles, body, ignoreInitial: true);
            if (upward != null) continue;
            Collider? front = Sweep(bounds.center + lift, extents, forward, advance, obstacles, body, ignoreInitial: false);
            if (front != null) continue;
            bool clear = true;
            foreach (Collider collider in Physics.OverlapBox(bounds.center + lift + horizontal, extents, Quaternion.identity,
                         obstacles, QueryTriggerInteraction.Ignore))
                if (collider.attachedRigidbody != body)
                { clear = false; break; }
            if (!clear) continue;
            body.position += lift + horizontal;
            // Settle on the verified tread before resuming the refreshed path.
            body.velocity = Vector3.zero;
            return true;
        }
        return false;
    }

    private static Collider? Sweep(Vector3 center, Vector3 extents, Vector3 direction, float distance,
        int mask, Rigidbody body, bool ignoreInitial)
    {
        foreach (RaycastHit hit in Physics.BoxCastAll(center, extents, direction, Quaternion.identity,
                     distance, mask, QueryTriggerInteraction.Ignore))
            if (hit.collider.attachedRigidbody != body && (!ignoreInitial || hit.distance > .001f)) return hit.collider;
        return null;
    }
    private static bool BodyBounds(Rigidbody body, Collider[] colliders, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
            if (!found) { bounds = collider.bounds; found = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        return found;
    }

    private static bool Support(Rigidbody body, Bounds bounds, out RaycastHit support)
    {
        support = default;
        bool found = false;
        Vector3 foot = new(bounds.center.x, bounds.min.y, bounds.center.z);
        // When the centre is over a riser/edge, another part of the feet can
        // already rest on the next tread. Use the highest floor under the body.
        for (int probe = 0; probe < 5; probe++)
        {
            Vector3 point = foot;
            if (probe == 1) point.x += bounds.extents.x * .7f;
            if (probe == 2) point.x -= bounds.extents.x * .7f;
            if (probe == 3) point.z += bounds.extents.z * .7f;
            if (probe == 4) point.z -= bounds.extents.z * .7f;
            if (!Nearest(point + Vector3.up * .08f, Vector3.down, .18f, body, out RaycastHit hit) ||
                !Terrain(hit, body) || hit.normal.y < .7f || hit.point.y > foot.y + .03f) continue;
            if (!found || hit.point.y > support.point.y) { support = hit; found = true; }
            if (probe == 0 && foot.y - hit.point.y <= .03f) return true; // Stable central support needs no extra rays.
        }
        return found;
    }

    private static bool Nearest(Vector3 origin, Vector3 direction, float distance, Rigidbody body, out RaycastHit nearest)
    {
        nearest = default;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            if (hit.collider.attachedRigidbody != body && (!found || hit.distance < nearest.distance))
            { nearest = hit; found = true; }
        return found;
    }

    private static bool Terrain(RaycastHit hit, Rigidbody body) => hit.collider.attachedRigidbody != body &&
        hit.collider.GetComponentInParent<PhysGrabObject>() == null && hit.collider.GetComponentInParent<Enemy>() == null &&
        hit.collider.GetComponentInParent<PlayerAvatar>() == null;
}
