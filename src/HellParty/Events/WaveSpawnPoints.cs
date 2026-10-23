using System.Collections.Generic;
using HellParty.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Events;

internal static class WaveSpawnPoints
{
    private static StageRuntime? _stage;
    private static bool _prepared;
    private static NavMeshTriangulation _mesh;
    private static readonly List<Area> Areas = new();
    private static readonly Collider[] ClearanceHits = new Collider[64];

    private sealed class Area
    {
        internal Vector3 Center;
        internal readonly List<int> Triangles = new();
        internal readonly List<Search> Searches = new();
    }

    private sealed class Search
    {
        internal int AgentType, AreaMask;
        internal float Height, Radius;
        internal float RetryAt;
        internal readonly List<Vector3> Reusable = new();
        internal readonly List<Candidate> Candidates = new(WaveSpawnRules.CandidateLimit);
    }

    private readonly struct Candidate
    {
        internal readonly Vector3 Position;
        internal readonly float Distance;
        internal Candidate(Vector3 position, float distance) { Position = position; Distance = distance; }
    }

    internal static void PrepareStage()
    {
        if (_prepared && _stage == StageRuntime.Current) return;
        _stage = StageRuntime.Current;
        _mesh = NavMesh.CalculateTriangulation();
        Areas.Clear();
        _prepared = true;
    }
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
        float height = Mathf.Max(.3f, settings.height);
        float radius = Mathf.Clamp(settings.radius, .12f, height * .5f);
        PrepareStage();
        Area area = GetArea(center);
        Search search = GetSearch(area, settings, height, radius);
        var players = new List<PlayerAvatar>();
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList()) if (Alive(player)) players.Add(player);
        if (players.Count == 0) { position = default; return false; }
        EnemyParent? ignore = agent != null ? agent.GetComponentInParent<EnemyParent>() : null;
        var path = new NavMeshPath();

        // A cache hit still needs current visibility, distance, clearance and
        // path checks. It avoids rebuilding and scanning the random pool.
        search.Reusable.Sort((a, b) => NearestDistance(a, players).CompareTo(NearestDistance(b, players)));
        foreach (Vector3 cached in search.Reusable)
            if (Validate(cached, center, filter, height, radius, players, ignore, path, out position)) return true;
        // Identical failed requests in the same tick must not repeat a full scan.
        if (Time.time < search.RetryAt) { position = default; return false; }

        search.Candidates.Clear();
        for (int i = 0; i < WaveSpawnRules.CandidateLimit && area.Triangles.Count > 0; i++)
        {
            int t = area.Triangles[Random.Range(0, area.Triangles.Count)];
            float a = Random.value, b = Random.value;
            if (a + b > 1) { a = 1 - a; b = 1 - b; }
            Vector3 v = _mesh.vertices[_mesh.indices[t]];
            Vector3 candidate = v + a * (_mesh.vertices[_mesh.indices[t + 1]] - v) + b * (_mesh.vertices[_mesh.indices[t + 2]] - v);
            if ((candidate - center).sqrMagnitude > MonsterWaveClock.Radius * MonsterWaveClock.Radius) continue;
            search.Candidates.Add(new Candidate(candidate, NearestDistance(candidate, players)));
        }
        search.Candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        foreach (Candidate candidate in search.Candidates)
        {
            if (!Validate(candidate.Position, center, filter, height, radius, players, ignore, path, out position)) continue;
            if (!search.Reusable.Contains(position))
            {
                if (search.Reusable.Count >= WaveSpawnRules.CachedPointLimit) search.Reusable.RemoveAt(0);
                search.Reusable.Add(position);
            }
            return true;
        }
        search.RetryAt = Time.time + WaveSpawnRules.RetryInterval;
        position = default;
        return false;
    }

    private static Area GetArea(Vector3 center)
    {
        foreach (Area area in Areas) if (area.Center == center) return area;
        // Keep active + warning centres without rebuilding one for the other.
        if (Areas.Count >= 4) Areas.RemoveAt(0);
        var next = new Area { Center = center };
        for (int t = 0; t < _mesh.indices.Length; t += 3)
        {
            var bounds = new Bounds(_mesh.vertices[_mesh.indices[t]], Vector3.zero);
            bounds.Encapsulate(_mesh.vertices[_mesh.indices[t + 1]]);
            bounds.Encapsulate(_mesh.vertices[_mesh.indices[t + 2]]);
            if (bounds.SqrDistance(center) <= MonsterWaveClock.Radius * MonsterWaveClock.Radius) next.Triangles.Add(t);
        }
        Areas.Add(next);
        return next;
    }

    private static Search GetSearch(Area area, NavMeshAgent settings, float height, float radius)
    {
        foreach (Search search in area.Searches)
            if (search.AgentType == settings.agentTypeID && search.AreaMask == settings.areaMask &&
                search.Height == height && search.Radius == radius) return search;
        var next = new Search { AgentType = settings.agentTypeID, AreaMask = settings.areaMask, Height = height, Radius = radius };
        area.Searches.Add(next);
        return next;
    }

    private static float NearestDistance(Vector3 point, List<PlayerAvatar> players)
    {
        float best = float.PositiveInfinity;
        foreach (PlayerAvatar player in players) best = Mathf.Min(best, (point - player.transform.position).sqrMagnitude);
        return best;
    }

    private static bool Validate(Vector3 candidate, Vector3 center, NavMeshQueryFilter filter, float height, float radius,
        List<PlayerAvatar> players, EnemyParent? ignore, NavMeshPath path, out Vector3 position)
    {
        position = default;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, .2f, filter)) return false;
        candidate = hit.position;
        if ((candidate - center).sqrMagnitude > MonsterWaveClock.Radius * MonsterWaveClock.Radius ||
            !WaveSpawnRules.PlayerDistanceAllowed(NearestDistance(candidate, players)) ||
            MonsterWave.Current != null && MonsterWave.Current.Occupied(candidate, ignore)) return false;
        int count = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.up * (radius + .08f),
            candidate + Vector3.up * (height - radius + .08f), radius, ClearanceHits,
            LayerMask.GetMask("Default", "PhysGrabObject", "Enemy"), QueryTriggerInteraction.Ignore);
        if (count == ClearanceHits.Length) return false; // Cannot certify a truncated result.
        for (int i = 0; i < count; i++)
            if (ignore == null || !ClearanceHits[i].transform.IsChildOf(ignore.transform)) return false;
        if (Visible(candidate, height, radius)) return false;
        PlayerAvatar? target = Nearest(candidate);
        if (target == null || !NavMesh.SamplePosition(target.transform.position, out NavMeshHit destination, 3f, filter) ||
            !NavMesh.CalculatePath(candidate, destination.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        position = candidate;
        return true;
    }

    private static bool Visible(Vector3 floor, float height, float radius)
    {
        Vector3 center = floor + Vector3.up * (height * .5f);
        Vector3[] points = { floor + Vector3.up * .1f, center, floor + Vector3.up * (height + .08f),
            center + Vector3.right * radius, center - Vector3.right * radius,
            center + Vector3.forward * radius, center - Vector3.forward * radius };
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!Alive(player)) continue;
            if (player.localCamera == null) return true; // Unknown view cannot certify a blind spot.
            Transform eye = player.localCamera.GetOverrideTransform();
            foreach (Vector3 point in points)
            {
                // Remote peers send camera pose, but not FOV/aspect. Use the
                // same generous 170 x 140 envelope as the Stalking Doll.
                if (player.isLocal && Camera.main != null)
                {
                    Vector3 p = Camera.main.WorldToViewportPoint(point);
                    if (p.z <= 0 || p.x < -.05f || p.x > 1.05f || p.y < -.05f || p.y > 1.05f) continue;
                }
                else
                {
                    Vector3 p = eye.InverseTransformPoint(point);
                    if (!StalkingDollRules.InView(p.x, p.y, p.z, Mathf.Tan(85 * Mathf.Deg2Rad), Mathf.Tan(70 * Mathf.Deg2Rad))) continue;
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
