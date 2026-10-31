using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace RepoBots;

[HarmonyPatch]
internal static class OfflineLobbyDataPatch
{
    private static MethodBase _target;

    private static bool Prepare()
    {
        // R.E.P.O. 0.5.0 moved lobby functionality from SteamManager
        // to SteamLobbyManager. Resolve by name to keep compatibility.
        var type = AccessTools.TypeByName("SteamLobbyManager")
            ?? AccessTools.TypeByName("SteamManager");
        _target = type == null ? null : AccessTools.Method(type, "SetLobbyData");
        return _target != null;
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static bool Prefix()
    {
        return !PhotonNetwork.OfflineMode;
    }
}
