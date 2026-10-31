using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(GumballValuable), "CheckForLeftPlayers")]
internal static class GumballJoinPatch
{
	private static void Prefix(GumballValuable __instance)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		List<PlayerAvatar> list = SemiFunc.PlayerGetList();
		if (list == null || (Object)(object)__instance.hypnosisLine == (Object)null)
		{
			return;
		}
		while (__instance.playerInSight.Count < list.Count)
		{
			__instance.playerInSight.Add(item: false);
		}
		while (__instance.previouslySeenList.Count < list.Count)
		{
			__instance.previouslySeenList.Add(item: false);
		}
		while (__instance.hypnosisLines.Count < list.Count)
		{
			PlayerAvatar val = list[__instance.hypnosisLines.Count];
			GameObject val2 = Object.Instantiate<GameObject>(__instance.hypnosisLine, ((Component)__instance).transform.position, Quaternion.identity);
			if ((Object)(object)val != (Object)null && (Object)(object)val.localCamera != (Object)null)
			{
				val2.transform.SetParent(val.localCamera.GetOverrideTransform());
			}
			val2.transform.localPosition = Vector3.zero;
			val2.transform.localRotation = Quaternion.identity;
			EyeLines component = val2.GetComponent<EyeLines>();
			if ((Object)(object)component != (Object)null && (Object)(object)val != (Object)null)
			{
				component.InitializeLine(val);
			}
			val2.transform.SetParent(__instance.HypnosisLinesParenTransform);
			val2.SetActive(false);
			__instance.hypnosisLines.Add(val2);
		}
	}
}
