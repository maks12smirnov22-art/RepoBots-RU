using System;
using System.Reflection;
using UnityEngine;

namespace RepoBots;

// R.E.P.O. 0.5.0 renamed PlayerAvatar.collider to playerCollider.
// Keep the old name as a fallback for earlier game versions.
internal static class AvatarCompat
{
    private static FieldInfo _field;
    private static bool _looked;

    public static void SetCollider(PlayerAvatar avatar, Collider collider)
    {
        if (avatar == null)
        {
            return;
        }

        if (!_looked)
        {
            _looked = true;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            _field = typeof(PlayerAvatar).GetField("playerCollider", flags)
                ?? typeof(PlayerAvatar).GetField("collider", flags);

            if (_field == null)
            {
                Plugin.Log.LogError("PlayerAvatar collider field not found (tried playerCollider and collider).");
            }
            else
            {
                Plugin.Log.LogInfo("PlayerAvatar collider field: " + _field.Name);
            }
        }

        if (_field == null)
        {
            return;
        }

        try
        {
            _field.SetValue(avatar, collider);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("Couldn't assign PlayerAvatar." + _field.Name + ": " + ex);
        }
    }
}
