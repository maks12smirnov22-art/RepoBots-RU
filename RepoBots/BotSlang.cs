using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RepoBots;

internal static class BotSlang
{
	private static readonly (float chance, Regex find, string[] use)[] Swaps = new (float, Regex, string[])[23]
	{
		(0.9f, new Regex("\\bgoing to\\b"), new string[1] { "gonna" }),
		(0.9f, new Regex("\\bwant to\\b"), new string[1] { "wanna" }),
		(0.85f, new Regex("\\b(got to|have to)\\b"), new string[1] { "gotta" }),
		(0.85f, new Regex("\\bkind of\\b"), new string[1] { "kinda" }),
		(0.6f, new Regex("\\bbecause\\b"), new string[2] { "cuz", "bc" }),
		(0.35f, new Regex("\\bprobably\\b"), new string[2] { "prob", "prolly" }),
		(0.45f, new Regex("\\byou\\b"), new string[1] { "u" }),
		(0.35f, new Regex("\\byour\\b"), new string[1] { "ur" }),
		(0.35f, new Regex("\\byoure\\b"), new string[1] { "ur" }),
		(0.6f, new Regex("\\b(okay)\\b"), new string[3] { "ok", "k", "aight" }),
		(0.5f, new Regex("^yes$"), new string[4] { "yea", "ye", "yep", "yuh" }),
		(0.5f, new Regex("^no$"), new string[3] { "nah", "nope", "nahh" }),
		(0.5f, new Regex("\\breally\\b"), new string[3] { "rly", "hella", "lowkey" }),
		(0.3f, new Regex("\\bsomething\\b"), new string[2] { "smth", "somethin" }),
		(0.6f, new Regex("\\bthough\\b"), new string[1] { "tho" }),
		(0.5f, new Regex("\\bright now\\b"), new string[1] { "rn" }),
		(0.8f, new Regex("\\bi dont know\\b"), new string[1] { "idk" }),
		(0.8f, new Regex("\\bto be honest\\b"), new string[2] { "tbh", "ngl" }),
		(0.8f, new Regex("\\boh my god\\b"), new string[1] { "omg" }),
		(0.4f, new Regex("\\bnothing\\b"), new string[1] { "nothin" }),
		(0.4f, new Regex("\\bwhat\\b"), new string[2] { "wut", "wat" }),
		(0.35f, new Regex("\\bthanks\\b"), new string[2] { "ty", "thx" }),
		(0.5f, new Regex("\\bon my way\\b"), new string[1] { "omw" })
	};

	private static readonly Regex NegativeWords = new Regex("\\b(dead|died|die|dying|hurt|broke|broken|ow|ouch|help|chasing|lost|cant|stuck|great|rude|why|ugh|no|nah|again|bad|low|rip|scared|wtf|mean)\\b");

	private static readonly Regex GoodWords = new Regex("\\b(got|nice|delivered|found|lets|money|easy|ez|bet|done|cha|gg|win|worth|secured|rich)\\b");

	private static readonly Regex Opinion = new Regex("\\b(think|kinda|kind of|pretty|really|so|creepy|scary|weird|cursed|close|bad|mid|fire|hate|love|like|crazy|insane|wild|sus|dark|big|huge|heavy|tired|lost|fun|boring|ugly|cute|smells?|easy|hard)\\b");

	private static readonly Regex QuestionStart = new Regex("^(how|what|where|who|why|when|which|you|u|did|do|does|is|are|anyone|any|can|should)\\b");

	private static readonly Random Rng = new Random();

	private static float R => (float)Rng.NextDouble();

	private static int Range(int a, int b)
	{
		return Rng.Next(a, b);
	}

	private static string Pick(params string[] s)
	{
		return s[Range(0, s.Length)];
	}

	public static string Apply(string text, SlangStyle st, bool urgent, out string fix)
	{
		fix = null;
		if (st == null || string.IsNullOrEmpty(text) || text.StartsWith("*"))
		{
			return text;
		}
		float level = st.Level;
		string text2 = text;
		(float, Regex, string[])[] swaps = Swaps;
		for (int i = 0; i < swaps.Length; i++)
		{
			var (num, regex, use) = swaps[i];
			if (regex.IsMatch(text2) && R < num * level)
			{
				text2 = regex.Replace(text2, (Match _) => Pick(use), 1);
			}
		}
		if (urgent)
		{
			if (R < 0.25f * level && text2.Length < 30)
			{
				text2 = text2.ToUpperInvariant();
			}
			return text2;
		}
		int num2 = text2.Split(' ').Length;
		st.SincePet++;
		bool flag = st.SincePet >= 3;
		bool flag2 = QuestionStart.IsMatch(text2);
		bool flag3 = NegativeWords.IsMatch(text2);
		bool flag4 = !flag3 && GoodWords.IsMatch(text2);
		bool flag5 = Opinion.IsMatch(text2);
		if (R < 0.35f * level)
		{
			text2 = Regex.Replace(text2, "\\blets go$", (Match _) => "lets go" + new string('o', Range(2, 5)));
			text2 = Regex.Replace(text2, "^(no|bro|yo|nice|bruh)$", (Match m) => m.Value + new string(m.Value[m.Value.Length - 1], Range(2, 4)));
		}
		if (num2 >= 2 && R < 0.3f * level && !text2.StartsWith(st.Pet + " "))
		{
			string text3;
			if (flag && R < 0.5f && st.Pet != "lol" && st.Pet != "chat" && st.Pet != "yall")
			{
				text3 = st.Pet;
			}
			else if (flag3)
			{
				text3 = Pick("bro", "bruh", "dude", "nah", "man", "yo");
			}
			else if (flag4)
			{
				text3 = Pick("yo", "yooo", "ayy", "ok", "lets go");
			}
			else if (flag2)
			{
				text3 = Pick("yo", "ay", "aye", "wait");
			}
			else
			{
				text3 = (flag5 ? Pick("ngl", "lowkey", "honestly", "bro", "yo") : Pick("yo", "ok", "alright"));
			}
			if (flag2)
			{
				switch (text3)
				{
				case "ngl":
				case "lowkey":
				case "honestly":
					goto IL_04d8;
				}
			}
			text2 = text3 + " " + text2;
		}
		else if (num2 >= 2 && R < 0.3f * level)
		{
			string text4;
			if (flag && R < 0.5f && st.Pet != "guys")
			{
				text4 = st.Pet;
			}
			else if (flag2)
			{
				text4 = Pick("bro", "fr", "tho", "rn");
			}
			else if (flag3)
			{
				text4 = Pick("lol", "lmao", "bro", "smh", "rip");
			}
			else if (flag4)
			{
				text4 = Pick("lets go", "ez", "W", "lets gooo");
			}
			else
			{
				text4 = (flag5 ? Pick("fr", "ngl", "tbh", "lol", "fr fr", "no cap", "deadass") : null);
			}
			if (text4 == null)
			{
				text4 = "";
			}
			if (text4.Length > 0 && !text2.EndsWith(" " + text4))
			{
				text2 = text2 + " " + text4;
			}
		}
		goto IL_04d8;
		IL_04d8:
		if (Regex.IsMatch(text2, "(^|\\s)" + Regex.Escape(st.Pet) + "(\\s|$)"))
		{
			st.SincePet = 0;
		}
		if (R < st.Typos)
		{
			string[] array = text2.Split(' ');
			List<int> list = new List<int>();
			for (int num3 = 0; num3 < array.Length; num3++)
			{
				if (array[num3].Length >= 5 && Regex.IsMatch(array[num3], "^[a-z]+$"))
				{
					list.Add(num3);
				}
			}
			if (list.Count > 0)
			{
				int num4 = list[Range(0, list.Count)];
				string text5 = array[num4];
				int num5 = Range(1, text5.Length - 2);
				char[] array2 = text5.ToCharArray();
				ref char reference = ref array2[num5];
				ref char reference2 = ref array2[num5 + 1];
				char c = array2[num5 + 1];
				char c2 = array2[num5];
				reference = c;
				reference2 = c2;
				string text6 = new string(array2);
				if (text6 != text5)
				{
					array[num4] = text6;
					text2 = string.Join(" ", array);
					if (R < 0.5f)
					{
						fix = "*" + text5;
					}
				}
			}
		}
		return text2;
	}
}
