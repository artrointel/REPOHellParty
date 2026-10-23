using System.Collections.Generic;
using HellParty.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Events;

internal static class WaveSpawnPoints
{
    private static StageRuntime? _stage;
    private static Vector3 _center;
    private static NavMeshTriangulation _mesh;
    private static readonly List<int> Triangles = new();
    internal static bool Alive(PlayerAvatar player) => player != null && !player.deadSet && !player.isDisabled;

    internal static PlayerAvatar? Nearest(Vector3 point, float radius = float.PositiveInfinity)
    {
        PlayerAvatar? target = null;
        float best = radius * radius;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!Alive(player)) continue;
            float distance = (point - player.transform.position).sqrMagnitude;
            if (distance <= best) { best = distance; target = player; }
        }
        return target;
    }

    internal static bool Find(Vector3 center, out Vector3 position, NavMeshAgent? agent = null)
    {
        // Triangulation can include isolated islands and surfaces for other
        // agent types. A floor sample alone does not prove this enemy can chase.
        NavMeshAgent? settings = agent;
        if (settings == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Enemies/Enemy - Gnome");
            if (prefab != null) settings = prefab.GetComponentInChildren<NavMeshAgent>(true);
        }
        if (settings == null) { position = default; return false; }
        var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = settings.areaMask };
        var path = new NavMeshPath();
        // A single conservative capsule fits every kind used by this wave.
        // Sample actual level navigation, rather than arbitrary world coordinates.
        if (_stage != StageRuntime.Current || Triangles.Count == 0 || _center != center)
        {
            _stage = StageRuntime.Current; _center = center;
            _mesh = NavMesh.CalculateTriangulation();
            Triangles.Clear();
            for (int t = 0; t < _mesh.indices.Length; t += 3)
            {
                var bounds = new Bounds(_mesh.vertices[_mesh.indices[t]], Vector3.zero);
                bounds.Encapsulate(_mesh.vertices[_mesh.indices[t + 1]]);
                bounds.Encapsulate(_mesh.vertices[_mesh.indices[t + 2]]);
                if (bounds.SqrDistance(center) <= MonsterWaveClock.Radius * MonsterWaveClock.Radius) Triangles.Add(t);
            }
        }
        for (int i = 0; i < 240 && Triangles.Count > 0; i++)
        {
            int t = Triangles[Random.Range(0, Triangles.Count)];
            float a = Random.value, b = Random.value;
            if (a + b > 1) { a = 1 - a; b = 1 - b; }
            Vector3 v = _mesh.vertices[_mesh.indices[t]];
            Vector3 candidate = v + a * (_mesh.vertices[_mesh.indices[t + 1]] - v) + b * (_mesh.vertices[_mesh.indices[t + 2]] - v);
            if ((candidate - center).sqrMagnitude > MonsterWaveClock.Radius * MonsterWaveClock.Radius ||
                !NavMesh.SamplePosition(candidate, out NavMeshHit hit, .2f, filter)) continue;
            candidate = hit.position;
            if (Physics.CheckCapsule(candidate + Vector3.up * .7f, candidate + Vector3.up * 1.8f, .6f,
                    LayerMask.GetMask("Default", "PhysGrabObject", "Enemy"), QueryTriggerInteraction.Ignore) ||
                Nearest(candidate, 2) != null || MonsterWave.Current != null && MonsterWave.Current.Occupied(candidate) ||
                Visible(candidate)) continue;
            PlayerAvatar? target = Nearest(candidate);
            if (target == null || !NavMesh.SamplePosition(target.transform.position, out NavMeshHit destination, 3f, filter) ||
                !NavMesh.CalculatePath(candidate, destination.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            position = candidate;
            return true;
        }
        position = default;
        return false;
    }

    private static bool Visible(Vector3 floor)
    {
        Vector3[] points = { floor + Vector3.up * .15f, floor + Vector3.up * 1.2f, floor + Vector3.up * 2.4f,
            floor + Vector3.up * 1.2f + Vector3.right * .6f, floor + Vector3.up * 1.2f - Vector3.right * .6f,
            floor + Vector3.up * 1.2f + Vector3.forward * .6f, floor + Vector3.up * 1.2f - Vector3.forward * .6f };
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!Alive(player)) continue;
            if (player.localCamera == null) return true; // Unknown view cannot certify a blind spot.
            Transform eye = player.localCamera.GetOverrideTransform();
            foreach (Vector3 point in points)
            {
                // Remote peers do not send FOV. Require world occlusion for them.
                if (player.isLocal && Camera.main != null)
                {
                    Vector3 p = Camera.main.WorldToViewportPoint(point);
                    if (p.z <= 0 || p.x < -.05f || p.x > 1.05f || p.y < -.05f || p.y > 1.05f) continue;
                }
                bool blocked = false;
                Vector3 ray = point - eye.position;
                foreach (RaycastHit hit in Physics.RaycastAll(eye.position, ray.normalized, ray.magnitude,
                             SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.GetComponentInParent<PlayerAvatar>() != null ||
                        hit.transform.GetComponentInParent<PlayerController>() != null ||
                        hit.transform.GetComponentInParent<PlayerTumble>() != null) continue;
                    blocked = true;
                    break;
                }
                if (!blocked) return true;
            }
        }
        return false;
    }
}
