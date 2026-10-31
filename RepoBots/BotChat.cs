using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotChat
{
	public enum Ask
	{
		None,
		WhereAreYou,
		Doing,
		Health,
		Money,
		Monsters,
		Loot,
		WhereThing,
		WhoAreYou,
		Hello,
		Thanks,
		Insult,
		Sorry,
		Laugh,
		Stats,
		Unknown
	}

	public enum Cmd
	{
		None,
		Follow,
		Come,
		Hold,
		Release,
		Truck,
		Grab,
		Bring,
		CartHere,
		CartExtract,
		CartDrive,
		Revive,
		Activate,
		Help,
		GoThere,
		Map,
		Heal,
		Open,
		ShopStop,
		ShopBuy
	}

	private static readonly Regex AllWords = new Regex("\\b(everyone|everybody|guys|all of you|you all|yall|y'all|team|bots|squad|anyone|anybody|somebody|someone)\\b");

	private const string Boxy = "\\b(chests?|drawers?|cabinets?|cupboards?|wardrobes?|closets?|fridges?|lockers?|safes?|box|boxes|bins?|trash( ?cans?)?|bookshel(f|ves)|shel(f|ves)|containers?|toilets?|furniture)\\b";

	private static readonly KeyValuePair<string, string>[] CanonRules = new KeyValuePair<string, string>[28]
	{
		new KeyValuePair<string, string>("\\b(u)\\b", "you"),
		new KeyValuePair<string, string>("\\b(ya|yah)\\b", "you"),
		new KeyValuePair<string, string>("\\bur\\b", "your"),
		new KeyValuePair<string, string>("\\br\\b", "are"),
		new KeyValuePair<string, string>("\\b(pls|plz|plez|pleas)\\b", "please"),
		new KeyValuePair<string, string>("\\bgonna\\b", "going to"),
		new KeyValuePair<string, string>("\\bwanna\\b", "want to"),
		new KeyValuePair<string, string>("\\bgotta\\b", "got to"),
		new KeyValuePair<string, string>("\\blemme\\b", "let me"),
		new KeyValuePair<string, string>("\\bgimme\\b", "give me"),
		new KeyValuePair<string, string>("\\bwhere'?d\\b", "where did"),
		new KeyValuePair<string, string>("\\bwhats\\b", "what's"),
		new KeyValuePair<string, string>("\\bwheres\\b", "where's"),
		new KeyValuePair<string, string>("\\bthats\\b", "that's"),
		new KeyValuePair<string, string>("\\b(im|i am)\\b", "i'm"),
		new KeyValuePair<string, string>("\\b(youre|you are)\\b", "you're"),
		new KeyValuePair<string, string>("\\bwe are\\b", "we're"),
		new KeyValuePair<string, string>("\\bdont\\b", "don't"),
		new KeyValuePair<string, string>("\\bcant\\b", "can't"),
		new KeyValuePair<string, string>("\\blets\\b", "let's"),
		new KeyValuePair<string, string>("\\boutta\\b", "out of"),
		new KeyValuePair<string, string>("\\b(kinda|sorta)\\b", "kind of"),
		new KeyValuePair<string, string>("\\bthe track\\b", "the truck"),
		new KeyValuePair<string, string>("\\b(over hear|over hair)\\b", "over here"),
		new KeyValuePair<string, string>("\\b(come hear|come her)\\b", "come here"),
		new KeyValuePair<string, string>("\\bfollow be\\b", "follow me"),
		new KeyValuePair<string, string>("\\b(heel|heil) me\\b", "heal me"),
		new KeyValuePair<string, string>("\\bpick it\\b(?! up)", "pick it up")
	};

	private static readonly string[] FillerRules = new string[14]
	{
		"^(so|okay|ok|alright|all right|well|oh|and|hey|yo|hi|ugh|bro|dude|man|buddy|mate|bruh|guys)\\b", "\\b(um+|uh+|uhh+|er+|erm|hmm+|ah+)\\b", "(?<!\\b(you|i|we|they|would|do|does|don't|it|look|looks|feel|feels|something|nothing|anything|what's|that's)) like\\b", "\\b(bro|dude|buddy|mate|bruh)\\b", "\\bman$", "\\b(can|could|would|will|won't|wont|can't) (you|someone|somebody|anyone|anybody|one of you|you guys|yall|y'all|ya)( please)?( maybe)?\\b", "\\b(do|does) (you|anyone|anybody|someone|somebody) (know|remember)\\b", "\\b(do|would) (you|ya) mind\\b", "\\b(tell|show) (me|us)\\b", "\\bi (need|want|would like|'d like) (you|someone|somebody|one of you|you guys|yall|y'all)( guys)?( to)?\\b",
		"\\b(you should|you need to|you have to|you got to|you must|you gotta|try to|try and|go ahead and|make sure to|make sure you)\\b", "\\b(please|just|really|actually|kinda|kind of|maybe|real quick|quickly|quick|asap|right now|for me|for a (sec|second|bit|minute)|if you can|if you could|when you can|when you get a chance)\\b", "\\bnow$", "^(so|and|then|ok|okay)\\b"
	};

	private static readonly Regex[] Canon = BuildCanon();

	private static readonly Regex[] Filler = BuildFiller();

	private static readonly string[] Vocab = new string[27]
	{
		"follow", "revive", "rescue", "extraction", "extract", "explore", "monster", "monsters", "activate", "everyone",
		"everybody", "upgrade", "upgrades", "crystal", "crystals", "cabinet", "cabinets", "drawer", "drawers", "regroup",
		"valuables", "valuable", "health", "wardrobe", "locker", "lockers", "fridge"
	};

	private static readonly HashSet<string> RealWords = new HashSet<string>
	{
		"fellow", "hollow", "mellow", "yellow", "fallen", "remove", "revise", "health's", "wealth", "stealth",
		"lock", "locked", "locket", "frisbee", "rocker", "docker", "crystals", "drawn", "drawing", "upgraded",
		"explode", "explored"
	};

	private static readonly HashSet<string> NotNames = new HashSet<string>
	{
		"that", "this", "it", "him", "her", "them", "the", "his", "their", "my",
		"me", "over", "out", "back", "some", "stuff"
	};

	private static bool Has(string text, string pattern)
	{
		return Regex.IsMatch(text, pattern);
	}

	private static bool InShop()
	{
		if ((Object)(object)RunManager.instance != (Object)null)
		{
			return SemiFunc.RunIsShop();
		}
		return false;
	}

	public static string Normalize(string s)
	{
		s = s.ToLowerInvariant().Replace("’", "'");
		s = s.Replace("ё", "е");
        s = Regex.Replace(s, "[^a-zа-я0-9' ]+", " ");
        s = Regex.Replace(s, @"\bболт\b", "bolt");
        s = Regex.Replace(s, @"\bржав(ый|ого|ому|ым)?\b", "rusty");
		return Regex.Replace(s, "\\s+", " ").Trim();
	}

	private static Regex[] BuildCanon()
	{
		Regex[] array = new Regex[CanonRules.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new Regex(CanonRules[i].Key);
		}
		return array;
	}

	private static Regex[] BuildFiller()
	{
		Regex[] array = new Regex[FillerRules.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new Regex(FillerRules[i]);
		}
		return array;
	}

	private static int Distance(string a, string b)
	{
		if (Mathf.Abs(a.Length - b.Length) > 2)
		{
			return 9;
		}
		int[,] array = new int[a.Length + 1, b.Length + 1];
		for (int i = 0; i <= a.Length; i++)
		{
			array[i, 0] = i;
		}
		for (int j = 0; j <= b.Length; j++)
		{
			array[0, j] = j;
		}
		for (int k = 1; k <= a.Length; k++)
		{
			for (int l = 1; l <= b.Length; l++)
			{
				array[k, l] = Mathf.Min(Mathf.Min(array[k - 1, l] + 1, array[k, l - 1] + 1), array[k - 1, l - 1] + ((a[k - 1] != b[l - 1]) ? 1 : 0));
			}
		}
		return array[a.Length, b.Length];
	}

	private static string FixSpelling(string t)
	{
		string[] array = t.Split(' ');
		bool flag = false;
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i];
			if (text.Length < 5 || RealWords.Contains(text) || Array.IndexOf(Vocab, text) >= 0)
			{
				continue;
			}
			string[] vocab = Vocab;
			foreach (string text2 in vocab)
			{
				int num = ((text2.Length < 8) ? 1 : 2);
				if (Distance(text, text2) <= num)
				{
					array[i] = text2;
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			return t;
		}
		return string.Join(" ", array);
	}

	public static string Canonical(string t)
	{
		t = FixSpelling(t);
		for (int i = 0; i < Canon.Length; i++)
		{
			t = Canon[i].Replace(t, CanonRules[i].Value);
		}
		return Regex.Replace(t, "\\s+", " ").Trim();
	}

	public static string StripFiller(string t)
	{
		for (int i = 0; i < 2; i++)
		{
			Regex[] filler = Filler;
			for (int j = 0; j < filler.Length; j++)
			{
				t = Regex.Replace(filler[j].Replace(t, " "), "\\s+", " ").Trim();
			}
		}
		return t;
	}

	private static bool IsQuestion(string t)
	{
		return Has(t, "^(where|what|what's|why|who|how|which|when|where's|is|are|did|does|was|were|have you|has anyone|anyone seen|seen any|you seen)\\b");
	}

	public static void Understand(string bare, out Cmd cmd, out string cmdSubject, out Ask ask, out string askSubject)
	{
		cmd = Cmd.None;
		cmdSubject = null;
		ask = Ask.None;
		askSubject = null;
		if (bare.Length == 0)
		{
			ask = Ask.Hello;
			return;
		}
        if (TryRussian(bare, out cmd, out ask)) return;
		string text = Canonical(bare);
		string text2 = StripFiller(text);
		if (text2.Length == 0)
		{
			ask = ParseAsk(text, out askSubject);
			if (ask == Ask.Unknown)
			{
				ask = Ask.Hello;
			}
			return;
		}
		cmd = ParseCmd(text2, out cmdSubject);
		if (cmd == Cmd.None && text2 != text)
		{
			cmd = ParseCmd(text, out cmdSubject);
		}
		if (cmd == Cmd.None)
		{
			ask = ParseAsk(text, out askSubject);
			if (ask == Ask.Unknown && text2 != text)
			{
				ask = ParseAsk(text2, out askSubject);
			}
		}
	}

    // Russian phrases are resolved before the original English parser.
    // Keep the English parser untouched for backwards compatibility.
    private static bool TryRussian(string text, out Cmd cmd, out Ask ask)
    {
        cmd = Cmd.None;
        ask = Ask.None;
        if (!Regex.IsMatch(text, "[а-я]")) return false;
        if (Has(text, @"(не ходи за мной|хватит ходить за мной|отстань|свободен|занимайся своим|иди собирай|исследуй)")) cmd = Cmd.Release;
        else if (Has(text, @"(иди за мной|следуй за мной|ходи за мной|пошли со мной|держись рядом|прикрой меня)")) cmd = Cmd.Follow;
        else if (Has(text, @"(тележк|телег|тачк)"))
        {
            if (Has(text, @"(выход|эвакуац|извлечен|сдач)")) cmd = Cmd.CartExtract;
            else if (Has(text, @"(сюда|ко мне|принеси|притащи|подгони)")) cmd = Cmd.CartHere;
            else cmd = Cmd.CartDrive;
        }
        else if (Has(text, @"(иди к выходу|в грузовик|к грузовику|возвращайся в машину|уходим домой)")) cmd = Cmd.Truck;
        else if (Has(text, @"(подойди|иди сюда|ко мне|подбеги сюда)")) cmd = Cmd.Come;
        else if (Has(text, @"(стой|остановись|подожди|жди здесь|не двигайся)")) cmd = Cmd.Hold;
        // "Подними меня" is a revive request, not a generic grab order.
        else if (Has(text, @"(оживи|воскреси|подними меня)")) cmd = Cmd.Revive;
        else if (Has(text, @"(подними|возьми|подбери|хватай|подхвати)")) cmd = Cmd.Grab;
        else if (Has(text, @"(принеси|притащи|донеси|отнеси)")) cmd = Cmd.Bring;
        else if (Has(text, @"(помоги|помощь|выручай)")) cmd = Cmd.Help;
        else if (Has(text, @"(лечи|вылечи|подлечи|исцели)")) cmd = Cmd.Heal;

        else if (Has(text, @"(открой|открывай)")) cmd = Cmd.Open;
        else if (Has(text, @"(где ты|ты где|где вы)")) ask = Ask.WhereAreYou;
        else if (Has(text, @"(что делаешь|чем занят|как дела|как жизнь)")) ask = Ask.Doing;
        else if (Has(text, @"(как здоровье|сколько здоровья|сколько хп|ты цел)")) ask = Ask.Health;
        else if (Has(text, @"(где монстр|видел монстра|монстры|опасно тут)")) ask = Ask.Monsters;
        else if (Has(text, @"(сколько денег|сколько заработали|какая квота)")) ask = Ask.Money;
        else if (Has(text, @"(нашел что|нашли что|есть добыча)")) ask = Ask.Loot;
        else if (Has(text, @"(как тебя зовут|кто ты)")) ask = Ask.WhoAreYou;
        else if (Has(text, @"(привет|здорово|здарова|добрый день)")) ask = Ask.Hello;
        else if (Has(text, @"(спасибо|благодарю|молодец|красавчик)")) ask = Ask.Thanks;
        else if (Has(text, @"(извини|прости|сорян)")) ask = Ask.Sorry;
        return cmd != Cmd.None || ask != Ask.None;
    }

	public static Cmd ParseCmd(string t, out string subject)
	{
		subject = null;
		if (Has(t, "\\b(don'?t|dont|do not|stop|no more|quit) (buy|buying|shop|shopping|get anything|spend(ing)? (any|anything|money|more))\\b|\\bsave (the |our |your |some )?money\\b") || (InShop() && Has(t, "\\bput (it|that|everything|them|stuff|your stuff|it all) back\\b|\\bnothing else\\b")))
		{
			return Cmd.ShopStop;
		}
		bool flag = Has(t, "\\b(buy|purchase|shopping)\\b");
		if (!IsQuestion(t) && !Has(t, "^(should|do)\\b(?! your)") && (flag || (InShop() && Has(t, "\\b(get|grab)\\b"))))
		{
			string text;
			if (Has(t, "\\b(health|heal|healing|hp|med|meds|medkits?|health ?packs?)\\b"))
			{
				text = "health";
			}
			else if (Has(t, "\\bupgrades?\\b"))
			{
				text = "upgrade";
			}
			else if (Has(t, "\\b(crystals?|batter(y|ies)|power|charge|charging)\\b"))
			{
				text = "crystal";
			}
			else if (Has(t, "\\b(weapons?|guns?|grenades?|melee|bats?|swords?|mines?|something to fight)\\b"))
			{
				text = "weapon";
			}
			else
			{
				text = ((Has(t, "\\b(anything|stuff|whatever|something|some things|what we need|what you want|go shopping|shopping)\\b") | flag) ? "any" : null);
			}
			if (text != null)
			{
				subject = text;
				return Cmd.ShopBuy;
			}
		}
		if (IsQuestion(t))
		{
			return Cmd.None;
		}
		if (Has(t, "\\b(carry on|back to work|do your (own )?thing|you'?re (free|good to go|dismissed)|dismissed|at ease|never ?mind|nevermind|split up|(stop|quit|no need to|don'?t need to|don'?t) (following|follow)( me)?|leave me alone|go away|free roam|as you were|you can go|go do (your|whatever|what)|do whatever|go have fun|on your own|by yourself|spread out)\\b") || Has(t, "\\b(go|keep|start|continue) (explore|exploring|loot|looting|search|searching|scavenge|scavenging|look around|looking around)\\b") || Has(t, "\\bgo (find|look for|search for|get|grab|hunt for|collect) (some |more |the )?(loot|stuff|valuables?|treasure|money|things|items)\\b") || Has(t, "^(explore|resume|continue|keep going|keep looting|go loot|go explore)$"))
		{
			return Cmd.Release;
		}
		if ((Has(t, "\\b(go|get|head|run|back|return|retreat|hurry|everyone|come|move|race|sprint) (back )?(in(to)? |to |inside )?(the )?truck\\b|^(back )?(in(to)?|to|inside) (the )?truck\\b|\\bgo home\\b|\\btruck (now|time)\\b|^truck$|^(let'?s )?(head|go|get|run|hurry) back( now| home)?$|^let'?s head (back|out)\\b") || Has(t, "\\b(time to (go|leave|head out|dip|bounce|go home)|let'?s (leave|dip|bounce|head out|get out of here|go home|bail|get out|go back)|we'?re (leaving|done|heading out|out of here)|get out of here|head out|evacuate|we (got|have|need) to (go|leave))\\b") || (Has(t, "\\b(to|in|into|inside) (the )?truck\\b") && !Has(t, "\\b(that|this|it|head|cart|follow|bring|carry|take|put|drop)\\b"))) && !Has(t, "\\b(stay|wait|remain|camp|hide)\\b") && !Has(t, "\\b(the|his|her|their|my|that|a|(?!let)\\w+'s) (dead )?head\\b"))
		{
			return Cmd.Truck;
		}
		if (Has(t, "\\b(follow( me)?|come with( me)?|stick with me|stay with me|stay close|stay near me|on me|keep up|tag along|come along|cover me|guard me|protect me|watch my back|escort me|walk with me|go with me|join me|stay behind me|get behind me|be my bodyguard|you'?re with me|you'?re coming with me|stay (by|beside|next to|near) (me|my side)|by my side|(stick|stay|keep) close|stick together|stay together)\\b") || Has(t, "^(come on|let'?s (go|move|roll)( out| together)?|with me)$"))
		{
			return Cmd.Follow;
		}
		if (!Has(t, "\\bcart\\b") && Has(t, "\\b(turn on|switch on|power on|start( up)?|activate|open|hit|press|push|use|fire up|pull|do)\\b.*\\b(extraction|extract|button|lever)\\b|^(extract|start extracting|activate)$|\\bturn (the |that |this )?(extraction|extract|button)( point)? on\\b"))
		{
			return Cmd.Activate;
		}
		if (Has(t, "\\bcarts?\\b") && !Has(t, "\\b(grab|take|carry|bring|put|get|drop|throw|toss) (this|that|it|these|those|them)\\b.*\\b(to|in|into|inside) (the )?cart\\b"))
		{
			if (Has(t, "\\b(extraction|extract|deliver|cart in)\\b") && Has(t, "\\b(take|push|bring|wheel|move|get|roll|drive|haul|deliver)\\b|^cart to\\b"))
			{
				return Cmd.CartExtract;
			}
			if ((Has(t, "\\b(here|to me|over|this way|me|my way)\\b") && Has(t, "\\b(bring|get|push|wheel|move|fetch|roll|drag|pull)\\b")) || Has(t, "\\b(need|want) (the |a |that )?cart\\b|^cart (over )?here$|^(bring|fetch) (the |a |that )?cart$"))
			{
				return Cmd.CartHere;
			}
			if (Has(t, "\\b(drive|take|grab|get|push|use|handle|man|run|steer|control|pilot|on cart duty|in charge of|you'?re on)\\b"))
			{
				return Cmd.CartDrive;
			}
		}
		foreach (Match item in Regex.Matches(t, "\\b(?:revive|save|rescue|resurrect|bring back|bring|get|grab|fetch|pick up|carry)\\s+(\\w+)\\b"))
		{
			string value = item.Groups[1].Value;
			PlayerAvatar val = DeadNamed(value) ?? (value.EndsWith("s") ? DeadNamed(value.Substring(0, value.Length - 1)) : null);
			if ((Object)(object)val != (Object)null)
			{
				subject = val.playerName;
				return Cmd.Revive;
			}
		}
		if (Has(t, "\\b(revive|rescue|resurrect|bring (him|her|them) back|save (him|her|them))\\b") || Has(t, "\\b(get|grab|bring|pick up|carry|take|fetch) (the |that |his |her |their |my |\\w+'s )?(dead )?(\\w+'s )?(dead )?(head|body)\\b"))
		{
			return Cmd.Revive;
		}
		bool flag2 = HelpWords(t) || Has(t, "\\b(health|hp|heal|healing)\\b");
		if ((!flag2 && Has(t, "\\b(give|hand|pass|toss|throw) me\\b|\\b(give|hand|pass|toss|throw) (it|that|this|them) (to me|over|here|my way)\\b")) || (!flag2 && Has(t, "\\b(bring|carry|fetch|get)\\b") && Has(t, "\\b(to me|here|over here|my way|me)\\b") && !Has(t, "\\bget (back )?(over )?here\\b")))
		{
			return Cmd.Bring;
		}
		if (!flag2 && Has(t, "\\b(put|drop|throw|toss|dump) (this|that|it|these|those|them) (in|into|inside|on) (the )?(cart|extraction|truck)\\b"))
		{
			return Cmd.Grab;
		}
		if (!flag2 && Has(t, "\\b(grab|get|take|pick up|pick|carry|bring|fetch|haul|lift|move|collect|nab|snag|yoink|steal)\\b.*\\b(that|this|it|those|these|them)\\b") && !Has(t, "\\b(get|move) (it|that) (going|open|started)\\b"))
		{
			return Cmd.Grab;
		}
		if ((!flag2 && Has(t, "^(grab|take|carry|haul|collect|fetch|nab|snag|yoink|pick up) (the |a |an |some |my )?\\w+")) || (!flag2 && Has(t, "^(get|bring) (the|a|an|some|that|this) \\w+")))
		{
			return Cmd.Grab;
		}
		if (Has(t, "\\b(check|look at|open|use|pull up|bring up|read|get out) (the |your |a )?map\\b"))
		{
			return Cmd.Map;
		}
		if (Has(t, "\\b(chests?|drawers?|cabinets?|cupboards?|wardrobes?|closets?|fridges?|lockers?|safes?|box|boxes|bins?|trash( ?cans?)?|bookshel(f|ves)|shel(f|ves)|containers?|toilets?|furniture)\\b") && Has(t, "\\b(open|loot|search|raid|check|look in|look inside|look through|go through|rummage|dig through|peek in|empty)\\b"))
		{
			subject = (Has(t, "\\b(all|every|everything|these|those|them|em|drawers|cabinets|chests|cupboards|wardrobes|lockers|fridges|safes|bins|boxes|containers|closets|shelves|bookshelves|toilets)\\b") ? "all" : null);
			return Cmd.Open;
		}
		if (Has(t, "\\b(loot|search|raid|check|clear) (this|the|that) (room|place|area)\\b|\\b(open|loot|search) (everything|all of it|all of them)\\b"))
		{
			subject = "all";
			return Cmd.Open;
		}
		if (Has(t, "\\b(open|loot|search|raid|look inside|check inside|look in)\\b.*\\b(that|this|it)\\b"))
		{
			return Cmd.Open;
		}
		if (Has(t, "\\b(go|move|stand|run|walk|head|get|wait|stay) (over )?(there|that way|to that spot|to that|where i'?m (looking|pointing))\\b|\\b(go )?(check|look) (it )?(out )?over there\\b|\\b(go|check|look|move|run|walk|head)\\b.*\\bover there\\b") && !Has(t, "\\b(wait|stay) (right )?there$"))
		{
			return Cmd.GoThere;
		}
		Match match = Regex.Match(t, "\\bheal\\s+(\\w+)");
		if (match.Success && match.Groups[1].Value != "up")
		{
			subject = match.Groups[1].Value;
			return Cmd.Heal;
		}
		if (Has(t, "\\bheal me\\b|\\bheal up\\b|\\bneed (some |a )?(health|hp|healing|heal|medic)\\b|\\bi'?m (low|dying|hurt|almost dead|about to die|on low|weak|bleeding|at one hp)\\b|\\blow (on )?(health|hp)\\b|\\b(someone|anyone|you) heal\\b|^heal\\b|\\bmedic\\b|\\b(give|share) (me )?(some |a little )?(health|hp|heal)\\b"))
		{
			return Cmd.Heal;
		}
		if (HelpWords(t))
		{
			return Cmd.Help;
		}
		if (Has(t, "\\b(wait here|stay here|stay there|stay put|hold position|wait there|don'?t move|don'?t go|hold up|wait up|hold on|hang on|wait (a )?(sec|second|minute|moment)|wait for me|stop moving|stand still|stay right (there|here)|wait right (there|here)|stop right there|camp (here|there)|guard (this|here)|hold (this|the) (position|spot|room|door|area)|chill (here|there)|stay in here|stand by|stop there|hold it|hold (your |this |the )?(position|ground|spot)|stay where you( are|'re)|stay (in|inside) (this|the) \\w+)\\b") || Has(t, "^(stop|wait|stay|halt|hold|freeze|camp)( (right )?there| here)?$|^(wait|stop|hold on|hold up)( (wait|stop|hold on|hold up))+$"))
		{
			return Cmd.Hold;
		}
		if (Has(t, "\\b(come here|come to me|over here|get (back )?(over )?here|come back|come over|come closer|regroup|(group|gather|bunch|huddle|meet) up|this way|meet me|come find me|hurry (up )?(over )?here|come)\\b") || Has(t, "^(here|to me)$"))
		{
			return Cmd.Come;
		}
		return Cmd.None;
	}

	private static bool HelpWords(string t)
	{
		if (!Has(t, "\\bhelp\\b") || Has(t, "\\bwhere\\b"))
		{
			return Has(t, "\\b(give|lend) (me )?a hand\\b|\\bneed (a )?hand\\b|\\b(this|it|that)( thing| one)?( is|'s) (too |really |so |super |very |pretty )?heavy\\b|\\bcan'?t (lift|carry|move) (this|it)\\b|\\bneed (some )?(backup|assistance)\\b|\\bassist\\b");
		}
		return true;
	}

	public static Ask ParseAsk(string t, out string subject)
	{
		subject = null;
		if (Has(t, "\\bwhere (are|r) (you|u)( guys| all)?\\b|\\bwhere you at\\b|\\bwhere'?s everyone\\b|\\bwhere is everyone\\b|\\bwhere did (you|u|everyone|you guys) go\\b|\\bwhat room are you in\\b|^(you there|are you there)$|^where( is| are|'s| did| r)?( you| u)?( at| go)?$"))
		{
			return Ask.WhereAreYou;
		}
		Match match = Regex.Match(t, "\\bwhere('s| is| are|s| did| do| does| was| were| should| can)? (the |my |our |that |a |we |i )?(\\w+)");
		if (!match.Success)
		{
			match = Regex.Match(t, "\\b(?:which way (?:is|to)|how do (?:i|we) get to|(?:seen|spotted|found|know where) (?:the|my|our)) (the |my |our )?(\\w+)");
		}
		if (match.Success)
		{
			string value = match.Groups[match.Groups.Count - 1].Value;
			string text = ((match.Groups.Count > 2) ? match.Groups[match.Groups.Count - 2].Value.Trim() : "");
			switch (value)
			{
			default:
				if ((!(text == "we") && !(text == "i")) || !(value != "cart"))
				{
					if (value == "cart" || value == "carts")
					{
						subject = "cart";
						break;
					}
					if (value.StartsWith("extract"))
					{
						subject = "extraction";
						break;
					}
					switch (value)
					{
					case "truck":
					case "exit":
					case "ship":
					case "van":
						subject = "truck";
						break;
					default:
						{
							if (!value.StartsWith("monster") && !value.StartsWith("enem"))
							{
								switch (value)
								{
								case "it":
									break;
								default:
									goto IL_01c0;
								case "loot":
								case "stuff":
									goto IL_01da;
								}
							}
							subject = "monster";
							break;
						}
						IL_01da:
						subject = "loot";
						break;
						IL_01c0:
						if (!value.StartsWith("valuable") && !(value == "money"))
						{
							subject = value;
							break;
						}
						goto IL_01da;
					}
					break;
				}
				goto case "go";
			case "go":
			case "head":
			case "take":
			case "bring":
				subject = "extraction";
				break;
			}
			return Ask.WhereThing;
		}
		if (Has(t, "\\bwhat (are |r )?(you|u) (doing|up to|working on|carrying|holding|going to do)\\b|\\bwhat('s| is) going on\\b|\\bwhat are you guys doing\\b|\\bwhat you doing\\b|\\bwhat'?re you doing\\b|\\b(whatcha|watcha|wachu) doing\\b|\\bwyd\\b|\\bwhat'?s up\\b|\\bstatus\\b|\\breport\\b|\\bsitrep\\b|\\byou busy\\b|\\bwhat'?s your (plan|job|status)\\b"))
		{
			return Ask.Doing;
		}
		if (Has(t, "\\bhow many (times|deaths|levels|runs)\\b|\\b(times|often) (have |did )?(you|u) died\\b|\\byour (stats|record|history)\\b|\\bhow'?s (the|this) (save|run) (been|going)\\b"))
		{
			return Ask.Stats;
		}
		if (!IsQuestion(t) && !Has(t, "^(any|anything|anyone|seen|see|did|do|does|got|find|found|you|have|has|can|could|status|report)\\b") && t.Split(' ').Length > 3)
		{
			if (!Has(t, "\\b(thanks|thank you|good job|nice job|well done|nice one|nice work|good work|appreciate it)\\b"))
			{
				if (!Has(t, "\\b(sorry|my bad|oops)\\b"))
				{
					if (!Has(t, "\\b(stupid|dumb|idiot|useless|bad bot|you suck|noob|moron)\\b"))
					{
						if (!Has(t, "\\b(lol|lmao|haha+|hehe|rofl)\\b"))
						{
							return Ask.Unknown;
						}
						return Ask.Laugh;
					}
					return Ask.Insult;
				}
				return Ask.Sorry;
			}
			return Ask.Thanks;
		}
		if (Has(t, "\\b(health|hp|you ok|you okay|are you ok|you alright|how are you|you good|you hurt|are you (hurt|injured|dying|alive)|you alive)\\b"))
		{
			return Ask.Health;
		}
		if (Has(t, "\\b(how much|money|haul|cash|how are we doing|quota|goal|how close|are we (close|there|done)|did we (make|hit|reach))\\b"))
		{
			return Ask.Money;
		}
		if (Has(t, "\\b(monsters?|enem(y|ies)|danger|dangerous|see anything|anything (around|out there|nearby|near|in there)|anything scary|threats?|is it safe|safe here|(did|do) you (see|hear) (it|that|something|anything)|what was that|^what('s| is| was) that( noise| sound| thing)?$|hear (that|anything|something))\\b"))
		{
			return Ask.Monsters;
		}
		if (Has(t, "\\b(loot|valuables?|found anything|anything good|find anything|find something|treasure|got anything|anything (valuable|worth))\\b"))
		{
			return Ask.Loot;
		}
		if (Has(t, "\\b(who are you|introduce|about yourself|what are you like)\\b"))
		{
			return Ask.WhoAreYou;
		}
		if (Has(t, "\\b(thanks|thank you|thx|ty|good job|nice job|well done|nice one|gg|good bot|love you|nice|nice work|good work|appreciate it)\\b"))
		{
			return Ask.Thanks;
		}
		if (Has(t, "\\b(sorry|my bad|oops)\\b"))
		{
			return Ask.Sorry;
		}
		if (Has(t, "\\b(stupid|dumb|idiot|useless|bad bot|trash|you suck|garbage|noob|moron)\\b"))
		{
			return Ask.Insult;
		}
		if (Has(t, "\\b(lol|lmao|haha+|hehe|rofl)\\b"))
		{
			return Ask.Laugh;
		}
		if (Has(t, "^(hi|hello|hey|yo|sup|howdy|hiya|greetings)\\b"))
		{
			return Ask.Hello;
		}
		return Ask.Unknown;
	}

	private static PlayerAvatar DeadNamed(string word)
	{
		if ((Object)(object)GameDirector.instance == (Object)null || NotNames.Contains(word))
		{
			return null;
		}
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player != (Object)null && player.isDisabled && player.playerName != null && Normalize(player.playerName).StartsWith(word) && word.Length >= 3)
			{
				return player;
			}
		}
		return null;
	}

	private static bool Hears(BotBrain b, PlayerAvatar speaker)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = (((Object)(object)speaker.PlayerVisionTarget != (Object)null) ? speaker.PlayerVisionTarget.VisionTransform.position : (((Component)speaker).transform.position + Vector3.up * 1.5f));
		float num = Vector3.Distance(val, b.Position + Vector3.up * 1.5f);
		if (num < 12f)
		{
			return true;
		}
		if (num > 25f)
		{
			return false;
		}
		return !Physics.Linecast(val, b.Position + Vector3.up * 1.5f, LayerMask.GetMask(new string[1] { "Default" }), (QueryTriggerInteraction)1);
	}

	public static void Heard(PlayerAvatar speaker, string message)
	{
		if ((Object)(object)speaker == (Object)null || string.IsNullOrEmpty(message) || BotRegistry.IsBot(speaker) || !PhotonNetwork.IsMasterClient || BotRegistry.Bots.Count == 0)
		{
			return;
		}
		BotBrain.Remember(speaker.playerName, message);
		if (!Plugin.ChatCommands.Value || speaker.isDisabled || Normalize(message).Length == 0)
		{
			return;
		}
		List<string> list = SplitOrders(message);
		if (list.Count > 1)
		{
			HashSet<BotBrain> hashSet = new HashSet<BotBrain>();
			foreach (string item in list)
			{
				foreach (BotBrain brain in BotManager.Brains)
				{
					if (brain != null && NamedIn(Normalize(item), brain.Name))
					{
						hashSet.Add(brain);
					}
				}
			}
			Plugin.Log.LogInfo((object)string.Format("Split into {0} orders: {1}", list.Count, string.Join(" | ", list)));
			{
				foreach (string item2 in list)
				{
					HeardOne(speaker, item2, hashSet);
				}
				return;
			}
		}
		HeardOne(speaker, message, null);
	}

	private static bool NamedIn(string t, string name)
	{
		// Normalize both sides: the player may address a bot by its Russian or
		// English name, while Normalize() canonicalizes "Болт" to "bolt".
		string normalizedName = Normalize(name ?? "");
		return normalizedName.Length > 0 &&
			Regex.IsMatch(Normalize(t), "\\b" + Regex.Escape(normalizedName) + "\\b(?!')");
	}

	public static string BareOf(string t, IEnumerable<string> names)
	{
		string input = AllWords.Replace(t, " ");
		foreach (string name in names)
		{
			string normalizedName = Normalize(name ?? "");
			if (normalizedName.Length > 0)
			{
				input = Regex.Replace(input, "\\b" + Regex.Escape(normalizedName) + "\\b(?!')", " ");
			}
		}
		input = Regex.Replace(input, "^(hey|yo|ok|okay|please)\\s+|\\s+please$", " ");
		return Regex.Replace(input, "\\s+", " ").Trim();
	}

	public static void UnderstandMessage(string message, IEnumerable<string> names, out Cmd cmd, out string cmdSubject, out Ask ask, out string askSubject)
	{
		Understand(BareOf(Normalize(message), names), out cmd, out cmdSubject, out ask, out askSubject);
		if (cmd != Cmd.None)
		{
			return;
		}
		string[] array = Regex.Split(message, "[.!?;]+");
		if (array.Length < 2)
		{
			return;
		}
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = BareOf(Normalize(array2[i]), names);
			if (text.Length != 0)
			{
				Understand(text, out var cmd2, out var cmdSubject2, out var _, out var _);
				if (cmd2 != Cmd.None)
				{
					cmd = cmd2;
					cmdSubject = cmdSubject2;
					ask = Ask.None;
					askSubject = null;
					break;
				}
			}
		}
	}

	private static string WithoutWho(string t, IEnumerable<string> names)
	{
		t = AllWords.Replace(t, " ");
		foreach (string name in names)
		{
			string normalizedName = Normalize(name ?? "");
			if (normalizedName.Length > 0)
			{
				t = Regex.Replace(t, "\\b" + Regex.Escape(normalizedName) + "\\b(?!')", " ");
			}
		}
		return Regex.Replace(t, "\\s+", " ").Trim();
	}

	private static List<string> BotNames()
	{
		List<string> list = new List<string>();
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null)
			{
				list.Add(brain.Name);
			}
		}
		return list;
	}

	public static List<string> SplitOrders(string message, List<string> names = null)
	{
		names = names ?? BotNames();
		List<string> list = new List<string>();
		string text = "";
		bool flag = false;
		string[] array = Regex.Split(message, "[.;!?,]+|\\s+(?:and then|and|then|also|plus)\\s+", RegexOptions.IgnoreCase);
		foreach (string text2 in array)
		{
			string text3 = Normalize(text2);
			if (text3.Length != 0)
			{
				string text4 = WithoutWho(text3, names);
				if ((text4 != text3) & flag)
				{
					list.Add(text);
					text = "";
					flag = false;
				}
				text = ((text.Length == 0) ? text2.Trim() : (text + " " + text2.Trim()));
				if (StripFiller(Canonical(text4)).Length > 0)
				{
					flag = true;
				}
			}
		}
		if (text.Length > 0)
		{
			list.Add(text);
		}
		return list;
	}

	private static void HeardOne(PlayerAvatar speaker, string message, HashSet<BotBrain> claimed)
	{
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		string text = Normalize(message);
		if (text.Length == 0)
		{
			return;
		}
		List<BotBrain> list = new List<BotBrain>();
		List<BotBrain> list2 = new List<BotBrain>();
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null)
			{
				if (NamedIn(text, brain.Name))
				{
					list.Add(brain);
				}
				else if (Hears(brain, speaker))
				{
					list2.Add(brain);
				}
			}
		}
		bool flag = AllWords.IsMatch(text);
		bool flag2 = (list.Count > 0) | flag;
		List<string> list3 = new List<string>();
		foreach (BotBrain item in list)
		{
			list3.Add(item.Name);
		}
		UnderstandMessage(message, list3, out var cmd, out var cmdSubject, out var ask, out var askSubject);
		if (list.FindAll((BotBrain b) => !b.Alive).Count > 0 && (cmd == Cmd.None || cmd == Cmd.Revive))
		{
			List<string> list4 = new List<string>();
			foreach (BotBrain item2 in list)
			{
				if (item2.Alive)
				{
					list4.Add(item2.Name);
				}
			}
			UnderstandMessage(message, list4, out var cmd2, out var cmdSubject2, out var _, out var _);
			if (cmd2 == Cmd.Revive)
			{
				cmd = Cmd.Revive;
				cmdSubject = cmdSubject2 ?? cmdSubject;
				ask = Ask.None;
			}
		}
		if (cmd == Cmd.Revive)
		{
			List<BotBrain> list5 = list.FindAll((BotBrain b) => !b.Alive);
			if (cmdSubject == null && list5.Count > 0)
			{
				cmdSubject = list5[0].Name;
			}
			list.RemoveAll((BotBrain b) => !b.Alive);
			flag2 = (list.Count > 0) | flag;
		}
		List<BotBrain> list6 = new List<BotBrain>(list);
		if (flag)
		{
			foreach (BotBrain item3 in list2)
			{
				if (claimed == null || !claimed.Contains(item3))
				{
					list6.Add(item3);
				}
			}
		}
		if (list6.Count == 0)
		{
			if (cmd == Cmd.None && (ask == Ask.None || ask == Ask.Unknown || ask == Ask.Laugh || ask == Ask.Sorry || ask == Ask.Insult || ask == Ask.Thanks || ask == Ask.Hello || ask == Ask.WhoAreYou))
			{
				return;
			}
			BotBrain botBrain = null;
			float num = float.MaxValue;
			foreach (BotBrain item4 in list2)
			{
				if (item4.Alive)
				{
					float num2 = Vector3.Distance(item4.Position, ((Component)speaker).transform.position);
					if (num2 < num)
					{
						num = num2;
						botBrain = item4;
					}
				}
			}
			if (botBrain == null)
			{
				return;
			}
			list6.Add(botBrain);
		}
		if (cmd == Cmd.None && list6.Count > 0 && BotMood.TrySupport(list6[0], message)) return;
		if (cmd != Cmd.None)
		{
			int num3 = 0;
			foreach (BotBrain item5 in list6)
			{
				if (!item5.Alive)
				{
					if (list.Contains(item5))
					{
						item5.AnswerLater(0.5f, Ask.WhereAreYou, speaker, null);
					}
				}
				else
				{
					BotBrain.MuteReplies = num3 >= 2;
					if (!BotMood.RefuseOrder(item5)) Run(cmd, cmdSubject, item5, speaker, flag || list.Count > 1);
					num3++;
				}
			}
			BotBrain.MuteReplies = false;
		}
		else if ((BotAI.Enabled & flag2) && (ask == Ask.Unknown || ask == Ask.Hello || ask == Ask.WhoAreYou || ask == Ask.Insult || ask == Ask.Thanks || ask == Ask.Sorry || ask == Ask.Laugh))
		{
			BotAI.Reply(list6[0], speaker, message);
		}
		else
		{
			if (ask == Ask.Unknown && !flag2)
			{
				return;
			}
			float num4 = 0.3f;
			int num5 = 0;
			foreach (BotBrain item6 in list6)
			{
				if (num5 >= 4)
				{
					break;
				}
				item6.AnswerLater(num4, ask, speaker, askSubject);
				num4 += 2.2f;
				num5++;
			}
		}
	}

	private static void Run(Cmd cmd, string subject, BotBrain b, PlayerAvatar by, bool group)
	{
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		switch (cmd)
		{
		case Cmd.Release:
			b.ClearOrder(say: true);
			break;
		case Cmd.Follow:
			if (b.CurrentOrder != BotBrain.OrderKind.Follow || !((Object)(object)b.OrderedBy == (Object)(object)by))
			{
				b.OrderFollow(by);
			}
			else
			{
				b.SayAck("Already on it.");
			}
			break;
		case Cmd.Help:
			b.OrderFollow(by);
			break;
		case Cmd.Heal:
		{
			PlayerAvatar val5 = by;
			if (subject != null && subject != "me" && subject != "us")
			{
				foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
				{
					if ((Object)(object)player != (Object)null && player.playerName != null && Normalize(player.playerName).StartsWith(subject) && subject.Length >= 3)
					{
						val5 = player;
					}
				}
			}
			if (!((Object)(object)val5 == (Object)(object)b.Self))
			{
				b.OrderHeal(val5);
			}
			break;
		}
		case Cmd.Come:
			b.OrderCome(by);
			break;
		case Cmd.Hold:
			b.OrderHold(by);
			break;
		case Cmd.Truck:
			b.OrderTruck(by);
			break;
		case Cmd.Map:
			if (!b.CheckMap("asked to", () =>
			{
				b.SayAck("ok got it");
			}))
			{
				b.SayAck("cant right now");
			}
			break;
		case Cmd.GoThere:
		{
			Vector3? val6 = BotCommands.GazePoint(by);
			if (val6.HasValue)
			{
				b.OrderGoTo(by, val6.Value);
			}
			else
			{
				b.SayAck("Where?");
			}
			break;
		}
		case Cmd.Activate:
		{
			ExtractionPoint val4 = BotCommands.NearestIdleExtraction(b.Position);
			if ((Object)(object)val4 != (Object)null)
			{
				b.OrderActivate(by, val4);
			}
			else
			{
				b.SayAck("There's none left to turn on.");
			}
			break;
		}
		case Cmd.CartHere:
		case Cmd.CartExtract:
		case Cmd.CartDrive:
		{
			PhysGrabCart val2 = BotCommands.NearestCart(b.Position);
			if ((Object)(object)val2 == (Object)null)
			{
				b.SayAck("I don't know where a cart is.");
			}
			else if (cmd == Cmd.CartExtract && !BotBrainExtractionActive())
			{
				b.SayAck("No extraction's on yet.");
			}
			else
			{
				b.OrderCart(by, val2, cmd == Cmd.CartHere);
			}
			break;
		}
		case Cmd.Revive:
		{
			PlayerAvatar val3 = null;
			if (subject != null)
			{
				foreach (PlayerAvatar player2 in GameDirector.instance.PlayerList)
				{
					if ((Object)(object)player2 != (Object)null && player2.playerName == subject)
					{
						val3 = player2;
					}
				}
			}
			if ((Object)(object)val3 == (Object)null)
			{
				val3 = BotCommands.NearestDead(b.Position);
			}
			if ((Object)(object)val3 == (Object)null || !b.OrderRevive(by, val3))
			{
				b.SayAck("I don't see a head to bring back.");
			}
			break;
		}
		case Cmd.Open:
			b.OrderOpen(by, BotCommands.GazeObject(by), subject == "all");
			break;
		case Cmd.ShopStop:
		{
			BotBrain.ShopStopNow();
			BotBrain botBrain = b;
			string text;
			if (BotBrain.AnyBotPurchases)
			{
				text = "ok, putting my stuff back";
			}
			else
			{
				text = (SemiFunc.RunIsShop() ? "ok, not buying anything" : "ok, no shopping next time");
			}
			botBrain.SayAck(text);
			break;
		}
		case Cmd.ShopBuy:
			BotBrain.ShopWantNow(subject);
			b.SayAck(subject switch
			{
				"crystal" => "ok, crystals for the charger", 
				"upgrade" => "ok, upgrades", 
				"health" => "ok, health packs", 
				"any" => SemiFunc.RunIsShop() ? "ok, ill grab what we need" : "ok, ill get stuff at the shop", 
				_ => "ok, something to fight with", 
			});
			break;
		case Cmd.Grab:
		case Cmd.Bring:
		{
			PhysGrabObject val = BotCommands.GazeObject(by);
			if ((Object)(object)val == (Object)null)
			{
				b.SayAck((Random.value < 0.5f) ? "Grab what?" : "Which thing?");
				break;
			}
			if ((Object)(object)((Component)val).GetComponent<PhysGrabHinge>() != (Object)null)
			{
				b.OrderOpen(by, val, all: false);
				break;
			}
			PhysGrabCart component = ((Component)val).GetComponent<PhysGrabCart>();
			if ((Object)(object)component != (Object)null)
			{
				b.OrderCart(by, component, cmd == Cmd.Bring);
				break;
			}
			PlayerDeathHead component2 = ((Component)val).GetComponent<PlayerDeathHead>();
			if ((Object)(object)component2 != (Object)null && (Object)(object)component2.playerAvatar != (Object)null)
			{
				b.OrderRevive(by, component2.playerAvatar);
			}
			else
			{
				b.OrderGrab(by, val, cmd == Cmd.Bring);
			}
			break;
		}
		}
	}

	public static void HeardVoice(PlayerAvatar speaker, string text)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		if ((Object)(object)GameDirector.instance == (Object)null || (int)GameDirector.instance.currentState != 2)
		{
			return;
		}
		try
		{
			Heard(speaker, text);
		}
		catch (Exception arg)
		{
			Plugin.Log.LogError((object)$"Bot voice command failed: {arg}");
		}
	}

	private static bool BotBrainExtractionActive()
	{
		RoundDirector instance = RoundDirector.instance;
		if ((Object)(object)instance != (Object)null)
		{
			return instance.extractionPointActive;
		}
		return false;
	}
}
