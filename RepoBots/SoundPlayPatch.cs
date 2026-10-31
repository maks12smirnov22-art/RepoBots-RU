using System;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(Sound), "Play", new Type[]
{
	typeof(Vector3),
	typeof(float),
	typeof(float),
	typeof(float),
	typeof(float)
})]
internal static class SoundPlayPatch
{
	private static void Postfix(AudioSource __result, Vector3 position)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)__result == (Object)null) && BotSenses.Listening)
		{
			BotSenses.Report(position, __result.maxDistance);
		}
	}
}
