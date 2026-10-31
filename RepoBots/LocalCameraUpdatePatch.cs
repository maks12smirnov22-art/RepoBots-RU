using HarmonyLib;
using UnityEngine;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerLocalCamera), "Update")]
internal static class LocalCameraUpdatePatch
{
	private static bool Prefix(PlayerLocalCamera __instance)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		BotInfo botInfo = BotRegistry.Get(__instance.playerAvatar);
		if (botInfo == null)
		{
			return true;
		}
		Transform transform = ((Component)__instance).transform;
		Vector3 val = __instance.playerAvatar.clientPositionCurrent + Vector3.up * (botInfo.Crouching ? 1f : 1.5f);
		transform.position = Vector3.Lerp(transform.position, val, 20f * Time.deltaTime);
		transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(botInfo.Pitch, botInfo.Yaw, 0f), 20f * Time.deltaTime);
		if (Vector3.Distance(transform.position, val) > 10f)
		{
			transform.position = val;
		}
		return false;
	}
}
