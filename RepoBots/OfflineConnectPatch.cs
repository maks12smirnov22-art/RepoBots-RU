using System;
using HarmonyLib;
using Photon.Pun;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(NetworkConnect), "Start")]
internal static class OfflineConnectPatch
{
	private static bool Prefix(NetworkConnect __instance)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		if (!BotOffline.Pending)
		{
			BotOffline.Active = false;
			return true;
		}
		BotOffline.Pending = false;
		PhotonNetwork.NickName = (SteamClient.IsValid ? SteamClient.Name : Environment.MachineName);
		PhotonNetwork.AutomaticallySyncScene = false;
		DataDirector.instance.PhotonSetRegion();
		DataDirector.instance.PhotonSetVersion();
		DataDirector.instance.PhotonSetAppId();
		if ((Object)(object)__instance.punVoiceClient != (Object)null)
		{
			Object.Instantiate<GameObject>(__instance.punVoiceClient, Vector3.zero, Quaternion.identity);
		}
		PhotonNetwork.Disconnect();
		((MonoBehaviour)__instance).StartCoroutine(BotOffline.GoOffline(__instance));
		return false;
	}
}
