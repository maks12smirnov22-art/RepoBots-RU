using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerAvatar), "Awake")]
internal static class AvatarAwakePatch
{
	private static bool Prefix(PlayerAvatar __instance)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		PhotonView component = ((Component)__instance).GetComponent<PhotonView>();
		BotInfo botInfo = BotRegistry.TryRegister(__instance, component);
		if (botInfo == null)
		{
			return true;
		}
		__instance.rb = ((Component)__instance).GetComponent<Rigidbody>();
		__instance.photonView = component;
		AvatarCompat.SetCollider(__instance, ((Component)__instance).GetComponentInChildren<Collider>());
		__instance.isDisabled = false;
		((Component)__instance).transform.position = Vector3.forward * 2f;
		__instance.playerAvatarCollision = ((Component)__instance).GetComponent<PlayerAvatarCollision>();
		GameDirector.instance.PlayerList.Add(__instance);
		__instance.isLocal = false;
		BotNet.RegisterWithEnemies(__instance);
		__instance.playerName = botInfo.Name;
		__instance.steamID = botInfo.SteamId;
		if ((Object)(object)StatsManager.instance != (Object)null)
		{
			StatsManager.instance.PlayerAdd(botInfo.SteamId, botInfo.Name);
		}
		Plugin.Log.LogInfo((object)$"Bot avatar ready: {botInfo.Name} (view {component.ViewID})");
		return false;
	}
}
