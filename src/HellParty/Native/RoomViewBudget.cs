using System;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty.Native;

internal sealed class RoomViewCapacityException : InvalidOperationException
{
    internal RoomViewCapacityException(string path) : base($"Room PhotonView budget is full; deferred {path}.") { }
}

internal static class RoomViewBudget
{
    private static double _warnAt;
    internal static void Ensure(GameObject prefab, string path)
    {
        if (!SemiFunc.IsMultiplayer()) return;
        int used = 0;
        // Count room IDs, not player-owned views. Do this before PUN creates a
        // prefab: partial allocation failure otherwise leaves an inactive clone.
        foreach (PhotonView view in PhotonNetwork.PhotonViewCollection)
            if (view != null && view.ViewID > 0 && view.ViewID < PhotonNetwork.MAX_VIEW_IDS) used++;
        int required = prefab.GetComponentsInChildren<PhotonView>(true).Length;
        if (!RoomViewBudgetRules.Fits(PhotonNetwork.MAX_VIEW_IDS - 1, used, required))
            throw new RoomViewCapacityException(path);
    }

    internal static void Warn()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < _warnAt) return;
        _warnAt = now + 10;
        Plugin.ModLogger.LogWarning("Room PhotonView budget is low. Extra summons are delayed or skipped; parked wave/Cleanup Crew summons are reused first.");
    }
}
