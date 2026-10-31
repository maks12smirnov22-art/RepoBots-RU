using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Networking;

namespace RepoBots;

internal static class BotAI
{
	private const string Url = "https://api.anthropic.com/v1/messages";

	private static int _inFlight;

	public static bool Enabled => !string.IsNullOrWhiteSpace(Plugin.AIApiKey.Value);

	public static void Reply(BotBrain bot, PlayerAvatar from, string message)
	{
		if (!Enabled || (Object)(object)BotManager.Runner == (Object)null)
		{
			bot.AnswerLater(0.3f, BotChat.Ask.Unknown, from, null);
		}
		else if (_inFlight < 2)
		{
			BotManager.Runner.StartCoroutine(Ask(bot, from, message));
		}
	}

	private static IEnumerator Ask(BotBrain bot, PlayerAvatar from, string message)
	{
		_inFlight++;
		string text = bot.AIBriefing(from);
		JObject val = new JObject
		{
			["model"] = new JValue(Plugin.AIModel.Value),
			["max_tokens"] = new JValue(120),
			["system"] = new JValue(text)
		};
		JArray val2 = new JArray();
		val2.Add((JToken)new JObject
		{
			["role"] = new JValue("user"),
			["content"] = new JValue(from.playerName + " says to you: \"" + message + "\"")
		});
		val["messages"] = (JToken)val2;
		JObject val3 = val;
		byte[] bytes = Encoding.UTF8.GetBytes(((JToken)val3).ToString((Formatting)0, Array.Empty<JsonConverter>()));
		UnityWebRequest req = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST");
		try
		{
			req.uploadHandler = (UploadHandler)new UploadHandlerRaw(bytes);
			req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
			req.SetRequestHeader("content-type", "application/json");
			req.SetRequestHeader("x-api-key", Plugin.AIApiKey.Value.Trim());
			req.SetRequestHeader("anthropic-version", "2023-06-01");
			req.timeout = 12;
			yield return req.SendWebRequest();
			_inFlight--;
			if ((int)req.result != 1)
			{
				ManualLogSource log = Plugin.Log;
				object arg = req.responseCode;
				string error = req.error;
				DownloadHandler downloadHandler = req.downloadHandler;
				log.LogWarning((object)$"Claude reply failed ({arg}): {error} {Truncate((downloadHandler != null) ? downloadHandler.text : null, 300)}");
				bot.AnswerLater(0f, BotChat.Ask.Unknown, from, null);
				yield break;
			}
			string text2 = null;
			try
			{
				foreach (JToken item in (IEnumerable<JToken>)(((object)JObject.Parse(req.downloadHandler.text)["content"]) ?? ((object)new JArray())))
				{
					if ((string)item[(object)"type"] == "text")
					{
						text2 += (string)item[(object)"text"];
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning((object)("Couldn't read Claude's reply: " + ex.Message));
			}
			if (string.IsNullOrWhiteSpace(text2))
			{
				yield break;
			}
			bot.FromAI(text2.Trim(), from);
		}
		finally
		{
			((IDisposable)req)?.Dispose();
		}
	}

	private static string Truncate(string s, int n)
	{
		if (s != null)
		{
			if (s.Length > n)
			{
				return s.Substring(0, n);
			}
			return s;
		}
		return "";
	}

	public static string TakeAction(ref string text)
	{
		Match match = Regex.Match(text, "\\[(FOLLOW|COME|WAIT|FREE|TRUCK|GRAB|BRING|CART|NONE)\\]", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "\\[[A-Z_]+\\]", "", RegexOptions.IgnoreCase).Trim();
		if (!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value.ToUpperInvariant();
	}
}
