using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]
internal static class HurtColliderPatch
{
	private static float _logAt;

	internal static PlayerAvatar HittingBot;

	private static void Prefix(HurtCollider __instance, PlayerAvatar _player, out bool __state)
	{
		__state = false;
		if (!((Object)(object)_player == (Object)null) && PhotonNetwork.IsMasterClient && BotRegistry.IsBot(_player) && !_player.photonView.IsMine)
		{
			if (__instance.playerDamageCooldown > 0f && (__instance.playerDamage > 0 || __instance.playerKill))
			{
				BotManager.BrainOf(_player)?.HurtZone(__instance);
			}
			_player.photonView.IsMine = true;
			__state = true;
			HittingBot = _player;
			if (Time.time >= _logAt)
			{
				_logAt = Time.time + 2f;
				Plugin.Log.LogInfo((object)(_player.playerName + " is in a hurt zone"));
			}
		}
	}

	private static void Finalizer(PlayerAvatar _player, bool __state)
	{
		if (__state && (Object)(object)_player != (Object)null)
		{
			_player.photonView.IsMine = false;
		}
		if (__state)
		{
			HittingBot = null;
		}
	}
}
