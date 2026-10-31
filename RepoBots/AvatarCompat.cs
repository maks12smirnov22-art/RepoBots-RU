using System.Reflection;
using UnityEngine;

namespace RepoBots;

// The game (v0.5.0) no longer has a field called PlayerAvatar.collider.
// Set it only if it still exists, so the player's own avatar can start.
internal static class AvatarCompat
{
	private static FieldInfo _field;

	private static bool _looked;

	public static void SetCollider(PlayerAvatar avatar, Collider collider)
	{
		if (!_looked)
		{
			_looked = true;
			_field = typeof(PlayerAvatar).GetField("collider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (_field == null)
			{
				Plugin.Log.LogWarning((object)"PlayerAvatar.collider not found in this game version, skipping it.");
			}
		}
		if (_field != null)
		{
			_field.SetValue(avatar, collider);
		}
	}
}
