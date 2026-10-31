using System;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerAvatar), "ChatMessageSendRPC")]
internal static class ChatHeardPatch
{
	private static void Postfix(PlayerAvatar __instance, string _message)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		BotListen.OthersSaid(_message);
		if (!PhotonNetwork.IsMasterClient || (Object)(object)GameDirector.instance == (Object)null || (int)GameDirector.instance.currentState != 2)
		{
			return;
		}
		try
		{
			BotChat.Heard(__instance, _message);
		}
		catch (Exception arg)
		{
			Plugin.Log.LogError((object)$"Bot chat failed: {arg}");
		}
	}
}
