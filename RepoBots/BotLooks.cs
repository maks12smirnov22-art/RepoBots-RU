using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotLooks
{
	internal static PlayerAvatar AvatarOf(PlayerCosmetics pc)
	{
		if ((Object)(object)pc == (Object)null)
		{
			return null;
		}
		if ((Object)(object)pc.deathHead != (Object)null && (Object)(object)pc.deathHead.playerAvatar != (Object)null)
		{
			return pc.deathHead.playerAvatar;
		}
		if (!((Object)(object)pc.playerAvatarVisuals != (Object)null))
		{
			return null;
		}
		return pc.playerAvatarVisuals.playerAvatar;
	}

	internal static List<int> CosmeticsFor(BotInfo info)
	{
		return new List<int>();
	}
}
