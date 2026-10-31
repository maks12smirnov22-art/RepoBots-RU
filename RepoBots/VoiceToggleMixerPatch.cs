using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "ToggleMixer")]
internal static class VoiceToggleMixerPatch
{
	private static bool Prefix(PlayerVoiceChat __instance, bool _lobby, bool _distorted)
	{
		if (!BotVoice.IsFake(__instance))
		{
			return true;
		}
		__instance.inLobbyMixer = _lobby;
		if ((Object)(object)__instance.audioSource != (Object)null)
		{
			__instance.audioSource.outputAudioMixerGroup = (_lobby ? __instance.mixerMicrophoneSpectate : (_distorted ? __instance.mixerMicrophoneSoundDistorted : __instance.mixerMicrophoneSound));
		}
		if (!_lobby && (Object)(object)__instance.ttsAudioSource != (Object)null)
		{
			__instance.ttsAudioSource.outputAudioMixerGroup = __instance.mixerTTSSound;
		}
		return false;
	}
}
