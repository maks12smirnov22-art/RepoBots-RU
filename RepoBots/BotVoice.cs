using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotVoice
{
	private static readonly Dictionary<PlayerVoiceChat, BotInfo> Fakes = new Dictionary<PlayerVoiceChat, BotInfo>();

	private static readonly float[] Spectrum = new float[1024];

	public static bool IsFake(PlayerVoiceChat v)
	{
		if ((Object)(object)v != (Object)null)
		{
			return Fakes.ContainsKey(v);
		}
		return false;
	}

	public static void Ensure(BotInfo info)
	{
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Expected Obj, but got Unknown
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = info.Avatar;
		if ((Object)(object)avatar == (Object)null || !SemiFunc.IsMultiplayer() || SemiFunc.MenuLevel() || (avatar.voiceChatFetched && (Object)(object)avatar.voiceChat != (Object)null))
		{
			return;
		}
		PlayerVoiceChat val = PlayerVoiceChat.instance;
		if ((Object)(object)val == (Object)null && (Object)(object)RunManager.instance != (Object)null)
		{
			foreach (PlayerVoiceChat voiceChat in RunManager.instance.voiceChats)
			{
				if ((Object)(object)voiceChat != (Object)null && !IsFake(voiceChat))
				{
					val = voiceChat;
				}
			}
		}
		if ((Object)(object)val == (Object)null || (Object)(object)val.TTSprefab == (Object)null)
		{
			return;
		}
		GameObject val2 = new GameObject("RepoBots voice " + info.Name);
		val2.SetActive(false);
		AudioSource val3 = val2.AddComponent<AudioSource>();
		val3.playOnAwake = false;
		val3.spatialBlend = 1f;
		val3.outputAudioMixerGroup = val.mixerMicrophoneSound;
		PlayerVoiceChat val4 = val2.AddComponent<PlayerVoiceChat>();
		Fakes[val4] = info;
		val4.TTSprefab = val.TTSprefab;
		val4.mixerMicrophoneSound = val.mixerMicrophoneSound;
		val4.mixerMicrophoneSoundDistorted = val.mixerMicrophoneSoundDistorted;
		val4.mixerMicrophoneSpectate = val.mixerMicrophoneSpectate;
		val4.mixerTTSSound = val.mixerTTSSound;
		val4.mixerTTSSpectate = val.mixerTTSSpectate;
		val4.audioSource = val3;
		val4.playerAvatar = avatar;
		GameObject val5 = Object.Instantiate<GameObject>(val.TTSprefab, val2.transform);
		val5.transform.localPosition = Vector3.zero;
		val5.transform.localRotation = Quaternion.identity;
		val4.ttsVoice = val5.GetComponent<TTSVoice>();
		val4.ttsAudioSource = (((Object)(object)val4.ttsVoice != (Object)null) ? ((Component)val4.ttsVoice).GetComponent<AudioSource>() : null);
		if ((Object)(object)val4.ttsVoice == (Object)null || (Object)(object)val4.ttsAudioSource == (Object)null)
		{
			Fakes.Remove(val4);
			Object.Destroy((Object)(object)val2);
			Plugin.Log.LogWarning((object)"Couldn't build a bot voice (TTS prefab changed?)");
			return;
		}
		val4.lowPassLogicTTS = ((Component)val4.ttsAudioSource).GetComponent<AudioLowPassLogic>();
		if ((Object)(object)val4.lowPassLogicTTS != (Object)null)
		{
			val4.lowPassLogicTTS.Fetch = true;
		}
		val4.ttsVoice.playerAvatar = avatar;
		val4.ttsAudioSource.outputAudioMixerGroup = val.mixerTTSSound;
		val4.TTSinstantiated = true;
		val4.inLobbyMixer = false;
		val4.inLobbyMixerTTS = false;
		val2.transform.position = ((Component)avatar).transform.position + Vector3.up * 1.5f;
		val2.SetActive(true);
		avatar.voiceChat = val4;
		avatar.voiceChatFetched = true;
		Plugin.Log.LogInfo((object)(info.Name + " has a voice"));
	}

	public static void Prune()
	{
		List<PlayerVoiceChat> list = null;
		foreach (KeyValuePair<PlayerVoiceChat, BotInfo> fake in Fakes)
		{
			if ((Object)(object)fake.Key == (Object)null || (Object)(object)fake.Value.Avatar == (Object)null)
			{
				(list ?? (list = new List<PlayerVoiceChat>())).Add(fake.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (PlayerVoiceChat item in list)
		{
			if ((Object)(object)item != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)item).gameObject);
			}
			Fakes.Remove(item);
		}
	}

	public static void Tick(PlayerVoiceChat v)
	{
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar playerAvatar = v.playerAvatar;
		if ((Object)(object)playerAvatar == (Object)null)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		v.OverridePitchLogic();
		v.OverrideVolumeStutterLogic();
		float num = 0f;
		if ((Object)(object)v.ttsVoice != (Object)null && (Object)(object)v.ttsAudioSource != (Object)null && v.ttsAudioSource.isPlaying)
		{
			v.ttsAudioSource.GetSpectrumData(Spectrum, 0, (FFTWindow)5);
			float num2 = 0f;
			float[] spectrum = Spectrum;
			foreach (float num3 in spectrum)
			{
				if (num3 > num2)
				{
					num2 = num3;
				}
			}
			num = num2 * 2f;
		}
		v.clipLoudnessTTS = num;
		v.clipLoudnessNoTTS = 0f;
		v.clipLoudness = num + v.overrideAddToClipLoudness;
		v.clipLoudnessCrawlingCounter = ((v.clipLoudness > 0.05f) ? (v.clipLoudnessCrawlingCounter + 1) : 0);
		if (v.clipLoudness > 0.005f)
		{
			if (!v.isTalking)
			{
				v.isTalkingStartTime = Time.time;
			}
			v.isTalking = true;
			v.isTalkingTimer = 0.5f;
		}
		else if (v.isTalkingTimer > 0f)
		{
			v.isTalkingTimer -= deltaTime;
			if (v.isTalkingTimer <= 0f)
			{
				v.isTalking = false;
			}
		}
		if (SemiFunc.IsMasterClient() && !playerAvatar.isDisabled && (playerAvatar.isCrawling ? (v.clipLoudnessCrawlingCounter > 10) : (playerAvatar.isCrouching ? (v.clipLoudness > 0.05f) : (v.clipLoudness > 0.025f))) && v.investigateTimer <= 0f && (Object)(object)EnemyDirector.instance != (Object)null)
		{
			v.investigateTimer = 1f;
			EnemyDirector.instance.SetInvestigate(playerAvatar.PlayerVisionTarget.VisionTransform.position, 5f, false);
		}
		if (v.investigateTimer >= 0f)
		{
			v.investigateTimer -= deltaTime;
		}
		if (v.SpatialDisableTimer > 0f || v.inLobbyMixer)
		{
			v.audioSource.spatialBlend = 0f;
			v.SpatialDisableTimer -= deltaTime;
		}
		else
		{
			v.audioSource.spatialBlend = 1f;
		}
		if ((Object)(object)v.lowPassLogicTTS != (Object)null)
		{
			v.lowPassLogicTTS.Volume = (playerAvatar.isCrouching ? 0.8f : 1f) * v.overrideVolumeStutterCurrent;
		}
		if (v.overrideMuteTimer > 0f)
		{
			v.overrideMuteTimer -= deltaTime;
			v.clipLoudness = 0f;
			v.isTalking = false;
			if ((Object)(object)v.lowPassLogicTTS != (Object)null)
			{
				v.lowPassLogicTTS.Volume = 0f;
			}
		}
		if (playerAvatar.isDisabled && (Object)(object)playerAvatar.playerDeathHead != (Object)null && (Object)(object)playerAvatar.playerDeathHead.physGrabObject != (Object)null)
		{
			((Component)v).transform.position = playerAvatar.playerDeathHead.physGrabObject.centerPoint;
		}
		if (v.overridePositionTimer > 0f)
		{
			v.overridePositionTimer -= deltaTime;
			if (playerAvatar.isDisabled)
			{
				((Component)v).transform.position = v.overridePosition;
			}
		}
		if (v.overrideNoTalkAnimationTimer > 0f)
		{
			v.overrideNoTalkAnimationTimer -= deltaTime;
		}
	}

	public static void Pitch(PlayerVoiceChat v)
	{
		if (Fakes.TryGetValue(v, out var value) && (Object)(object)v.ttsAudioSource != (Object)null)
		{
			AudioSource ttsAudioSource = v.ttsAudioSource;
			ttsAudioSource.pitch *= BotPersona.VoicePitch(value.SteamId);
		}
	}
}
