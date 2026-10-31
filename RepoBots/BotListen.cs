using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Windows.Speech;

namespace RepoBots;

internal class BotListen : MonoBehaviour
{
	public const byte VoiceEvent = 148;

	private DictationRecognizer _rec;

	private bool _broken;

	private int _fails;

	private float _restartAt;

	private bool _subscribed;

	private float _micOpenAt = -99f;

	private bool _uttOk;

	private string _shown;

	private float _shownUntil;

	private GUIStyle _style;

	private bool _micChecked;

	private float _hypLogAt = -99f;

	private static readonly List<KeyValuePair<string, float>> Recent = new List<KeyValuePair<string, float>>();

	private static readonly Dictionary<string, string> Misheard = new Dictionary<string, string>
	{
		["Cog"] = "cogg|cogs|kog|kogg|cogue",
		["Pip"] = "pipp|pep|pips|pippa",
		["Tater"] = "tator|tayter|tatar|tatter|tatum",
		["Dingus"] = "dinghus|dingis|dingess|dangus|dinkus|dingos",
		["Rusty"] = "rusti|rustie|rustee",
		["Gizmo"] = "gismo|gizmoe|gizmos",
		["Noodle"] = "noodles|nudle|newdle",
		["Widget"] = "widgit|wigit|widgets|wiget",
		["Sprocket"] = "sprockett|sprockets|spocket|procket",
		["Bolt"] = "bolts|boult|volt"
	};

	public static void OthersSaid(string text)
	{
		Recent.Add(new KeyValuePair<string, float>(BotChat.Canonical(BotChat.Normalize(text)), Time.time));
		if (Recent.Count > 20)
		{
			Recent.RemoveAt(0);
		}
	}

	private static bool Wanted()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		if (!Plugin.VoiceCommands.Value || BotRegistry.Bots.Count == 0)
		{
			return false;
		}
		GameDirector instance = GameDirector.instance;
		if ((Object)(object)instance != (Object)null && (int)instance.currentState == 2)
		{
			return (Object)(object)SemiFunc.PlayerAvatarLocal() != (Object)null;
		}
		return false;
	}

	private static bool MicOpen()
	{
		if ((Object)(object)DataDirector.instance != (Object)null && DataDirector.instance.toggleMute)
		{
			return false;
		}
		if ((Object)(object)AudioManager.instance != (Object)null && AudioManager.instance.pushToTalk && !SemiFunc.InputHold((InputKey)25))
		{
			return false;
		}
		PlayerAvatar val = SemiFunc.PlayerAvatarLocal();
		if ((Object)(object)val != (Object)null && (Object)(object)val.voiceChat != (Object)null && !val.voiceChat.microphoneEnabled)
		{
			return false;
		}
		return true;
	}

	private void Update()
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Invalid comparison between Unknown and I4
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		if (!_subscribed && PhotonNetwork.NetworkingClient != null)
		{
			PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
			_subscribed = true;
		}
		int num;
		if (!_broken)
		{
			num = (Wanted() ? 1 : 0);
			if (num != 0 && MicOpen())
			{
				_micOpenAt = Time.time;
			}
		}
		else
		{
			num = 0;
		}
		if (num == 0)
		{
			if (_rec != null && (int)_rec.Status == 1)
			{
				_rec.Stop();
			}
		}
		else
		{
			if (Time.time < _restartAt)
			{
				return;
			}
			try
			{
				if (_rec == null)
				{
					Create();
				}
				CheckMic();
				if (_rec != null && (int)_rec.Status == 0)
				{
					_rec.Start();
					Plugin.Log.LogInfo((object)"Voice commands: listening");
				}
			}
			catch (Exception ex)
			{
				Fail("couldn't start speech recognition: " + ex.Message);
			}
			_restartAt = Time.time + 1f;
		}
	}

	private void Create()
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected Obj, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected Obj, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected Obj, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected Obj, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected Obj, but got Unknown
		_rec = new DictationRecognizer((ConfidenceLevel)2, (DictationTopicConstraint)2);
		_rec.InitialSilenceTimeoutSeconds = 60f;
		_rec.AutoSilenceTimeoutSeconds = 60f;
		_rec.DictationHypothesis += (string text) =>
		{
			if (Time.time - _micOpenAt < 0.3f)
			{
				_uttOk = true;
			}
			if (Time.time - _hypLogAt > 60f)
			{
				_hypLogAt = Time.time;
				Plugin.Log.LogInfo((object)("Voice commands: hearing speech (\"" + text + "\")" + (_uttOk ? "" : " but the game mic is closed (mute / push to talk)")));
			}
		};
		_rec.DictationResult += OnResult;
		_rec.DictationComplete += (DictationCompletionCause cause) =>
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Invalid comparison between Unknown and I4
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Invalid comparison between Unknown and I4
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			if ((int)cause != 0 && (int)cause != 3 && (int)cause != 4 && (int)cause != 2)
			{
				Plugin.Log.LogInfo((object)$"Voice commands: recognizer stopped ({cause})");
			}
			_restartAt = Time.time + 0.3f;
		};
		_rec.DictationError += (string error, int hresult) =>
		{
			if (hresult == -2147199735 || error.IndexOf("rivacy", StringComparison.Ordinal) >= 0)
			{
				_broken = true;
				Plugin.Log.LogWarning((object)"Voice commands: Windows online speech recognition is off. Turn it on in Settings > Privacy & security > Speech.");
				Show("Voice commands need Windows online speech recognition: Settings > Privacy & security > Speech", 15f);
			}
			else
			{
				Fail($"{error} (0x{hresult:X8})");
			}
		};
	}

	private void CheckMic()
	{
		if (_micChecked)
		{
			return;
		}
		BotMicCheck.Start();
		if (!BotMicCheck.Done)
		{
			return;
		}
		_micChecked = true;
		string result = BotMicCheck.Result;
		string text = (((Object)(object)SessionManager.instance != (Object)null) ? SessionManager.instance.micDeviceCurrent : null);
		Plugin.Log.LogInfo((object)("Voice commands: Windows default mic is '" + (result ?? "?") + "', game mic is '" + (string.IsNullOrEmpty(text) ? "default" : text) + "'"));
		if (result != null)
		{
			bool flag = !string.IsNullOrEmpty(text) && !BotMicCheck.SameMic(text, result);
			if (flag || (string.IsNullOrEmpty(text) && BotMicCheck.LooksVirtual(result)))
			{
				string text2 = "Voice commands listen to the Windows default mic (" + result + ")" + (flag ? (", not your game mic (" + text + ")") : "") + ". Set your real mic as default in Windows Settings > System > Sound > Input.";
				Plugin.Log.LogWarning((object)("Voice commands: " + text2));
				Show(text2, 15f);
			}
		}
	}

	private void Fail(string why)
	{
		_fails++;
		Plugin.Log.LogWarning((object)("Voice commands: " + why));
		_restartAt = Time.time + 10f;
		if (_fails >= 5)
		{
			_broken = true;
			Plugin.Log.LogWarning((object)"Voice commands: giving up for this session");
			Show("Voice commands aren't working: " + why, 12f);
		}
	}

	private void OnResult(string text, ConfidenceLevel confidence)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Invalid comparison between Unknown and I4
		bool flag = _uttOk || Time.time - _micOpenAt < 1.5f;
		_uttOk = false;
		if (!flag || (int)confidence == 3 || string.IsNullOrWhiteSpace(text) || !Wanted())
		{
			Plugin.Log.LogInfo((object)("Voice commands: dropped \"" + text + "\" (" + ((!flag) ? "game mic was closed" : (((int)confidence == 3) ? "too unsure" : "not in a level")) + ")"));
		}
		else
		{
			_fails = 0;
			Heard(text.Trim());
		}
	}

	internal void Heard(string text)
	{
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected Obj, but got Unknown
		text = FixNames(text);
		if (IsEcho(text))
		{
			Plugin.Log.LogInfo((object)("Voice commands: ignored \"" + text + "\" (someone else's voice through the speakers)"));
			return;
		}
		Plugin.Log.LogInfo((object)("Voice commands: heard \"" + text + "\""));
		if (Plugin.VoiceShowText.Value)
		{
			Show("\"" + text + "\"", 3.5f);
		}
		PlayerAvatar val = SemiFunc.PlayerAvatarLocal();
		if (!((Object)(object)val == (Object)null))
		{
			if (PhotonNetwork.IsMasterClient)
			{
				BotChat.HeardVoice(val, text);
				return;
			}
			PhotonNetwork.RaiseEvent((byte)148, (object)new object[1] { text }, new RaiseEventOptions
			{
				Receivers = (ReceiverGroup)2
			}, SendOptions.SendReliable);
		}
	}

	private static void OnEvent(EventData ev)
	{
		if (ev.Code != 148 || !PhotonNetwork.IsMasterClient || !(ev.CustomData is object[] array) || array.Length < 1 || !(array[0] is string { Length: <=200 } text))
		{
			return;
		}
		foreach (PlayerAvatar item in SemiFunc.PlayerGetList())
		{
			if ((Object)(object)item != (Object)null && (Object)(object)item.photonView != (Object)null && item.photonView.Owner != null && item.photonView.Owner.ActorNumber == ev.Sender && !BotRegistry.IsBot(item))
			{
				BotChat.HeardVoice(item, text);
				break;
			}
		}
	}

	private static bool IsEcho(string text)
	{
		string[] array = BotChat.Canonical(BotChat.Normalize(text)).Split(' ');
		if (array.Length == 0 || array[0].Length == 0)
		{
			return false;
		}
		foreach (KeyValuePair<string, float> item2 in Recent)
		{
			if (Time.time - item2.Value > 4f + (float)item2.Key.Length * 0.08f)
			{
				continue;
			}
			HashSet<string> hashSet = new HashSet<string>(item2.Key.Split(' '));
			int num = 0;
			string[] array2 = array;
			foreach (string item in array2)
			{
				if (hashSet.Contains(item))
				{
					num++;
				}
			}
			if (num >= Mathf.Max(1, Mathf.CeilToInt((float)array.Length * 0.6f)))
			{
				return true;
			}
		}
		return false;
	}

	private static string FixNames(string text)
	{
		foreach (BotInfo value2 in BotRegistry.Bots.Values)
		{
			if (value2 != null && value2.Name != null && Misheard.TryGetValue(value2.Name, out var value))
			{
				text = Regex.Replace(text, "\\b(" + value + ")\\b", value2.Name, RegexOptions.IgnoreCase);
			}
		}
		return text;
	}

	private void Show(string text, float seconds)
	{
		_shown = text;
		_shownUntil = Time.time + seconds;
	}

	private void OnGUI()
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected Obj, but got Unknown
		if (_shown != null && !(Time.time > _shownUntil))
		{
			if (_style == null)
			{
				_style = new GUIStyle(GUI.skin.label)
				{
					alignment = (TextAnchor)4,
					fontSize = 18,
					wordWrap = true
				};
			}
			float num = Mathf.Clamp01((_shownUntil - Time.time) / 0.6f);
			Rect val = new Rect((float)Screen.width * 0.2f, (float)Screen.height * 0.78f, (float)Screen.width * 0.6f, 50f);
			GUI.color = new Color(0f, 0f, 0f, 0.7f * num);
			GUI.Label(new Rect(val.x + 1.5f, val.y + 1.5f, val.width, val.height), _shown, _style);
			GUI.color = new Color(1f, 1f, 1f, 0.9f * num);
			GUI.Label(val, _shown, _style);
			GUI.color = Color.white;
		}
	}

	private void OnDestroy()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		if (_rec != null)
		{
			if ((int)_rec.Status == 1)
			{
				_rec.Stop();
			}
			_rec.Dispose();
		}
	}
}
