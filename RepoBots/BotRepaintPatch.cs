using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerCosmetics), "SetupCosmeticsLogic")]
internal static class BotRepaintPatch
{
	private static void Postfix(PlayerCosmetics __instance)
	{
		if (BotRegistry.Get(BotLooks.AvatarOf(__instance)) == null || __instance.colorsEquipped == null)
		{
			return;
		}
		int[] colorsEquipped = __instance.colorsEquipped;
		for (int i = 0; i < colorsEquipped.Length; i++)
		{
			if (colorsEquipped[i] < 0)
			{
				return;
			}
		}
		__instance.SetupColorsLogic((int[])__instance.colorsEquipped.Clone());
	}
}
