using System.Collections.Generic;
using UnityEngine;

namespace RepoBots;

internal static class BotLines
{
	private static readonly Dictionary<string, string[]> Bank = new Dictionary<string, string[]>
	{
		["greet|slang"] = new string[10] { "aight lets get this bread", "we locked in", "ok chat lets cook", "lets get this W", "no dying this time fr", "its giving haunted house", "bet lets go", "we finna be rich", "ight whos carrying", "lock in boys" },
		["trucktalk|slang"] = new string[8] { "ngl that was kinda fire", "we ate that", "lowkey carried tbh", "that run was mid", "who threw tho", "im so tired chat", "gg ez", "W run" },
		["banter|slang"] = new string[10] { "this place is lowkey creepy", "nah this map is cursed", "who designed this place bro", "im lowkey lost", "ngl i hear things", "this is so sus", "its giving horror movie", "bro why is it so dark", "chat is this real", "my ping is cooked" },
		["botreply|slang"] = new string[11]
		{
			"fr", "real", "facts", "no cap", "ong", "bro what", "same lol", "deadass", "W", "L",
			"nah fr"
		},
		["spotreact|slang"] = new string[5] { "nah im good", "bro where", "aight avoiding that", "ty", "im not going there lol" },
		["spot|slang"] = new string[4] { "yo {monster} {where}", "{monster} {where} bro", "bro theres a {monster} {where}", "{monster} {where} watch out fr" },
		["flee|slang"] = new string[6] { "BRO ITS CHASING ME", "nah nah nah", "im cooked", "bro help", "its on me its on me", "RUN" },
		["hurtsmall|slang"] = new string[5] { "bruh", "ow bro", "ayo", "thats crazy", "ok ow" },
		["hurtbad|slang"] = new string[3] { "im cooked, {hp} hp", "bro im on {hp}", "im lowkey dying, {hp}" },
		["died|slang"] = new string[5] { "bro i died, heads {where}", "im cooked, head is {where}", "nah thats crazy, heads {where}", "L, heads {where}", "skill issue, heads {where}" },
		["revived|slang"] = new string[5] { "we back", "ty fr", "im back baby", "W revive", "ur goated for that" },
		["personrevived|slang"] = new string[3] { "{person} is back lets go", "welcome back {person}", "{person} back from the dead fr" },
		["haulbrag|slang"] = new string[4] { "yo this {item} is {money}", "{money} {item} lets go", "W, {item} worth {money}", "we rich, {money} {item}" },
		["delivered|slang"] = new string[4] { "{item} in, ez", "W", "another one", "secured the bag" },
		["needhelp|slang"] = new string[3] { "yo someone help me with this {item}", "bro this {item} is heavy", "need help fr" },
		["itembroke|slang"] = new string[4] { "oop", "my b", "that wasnt me lol", "nah i didnt do that" },
		["extractdone|slang"] = new string[4] { "lets gooo", "W", "ez money", "we ate" },
		["truck|slang"] = new string[4] { "aight truck time", "lets dip", "everyone to the truck fr", "we out" },
		["slacker|slang"] = new string[3] { "yo wait up", "dont leave me bro", "WAIT" },
		["lost|slang"] = new string[3] { "yall where u at", "bro where did everyone go", "hello" },
		["hello|slang"] = new string[4] { "yo", "sup", "wsg", "ayo {person}" },
		["laugh|slang"] = new string[5] { "lmaooo", "im crying", "bro", "lol", "dead" },
		["unknown|slang"] = new string[5] { "wut", "huh", "bro what", "say that again", "i didnt catch that lol" },
		["askthanks|slang"] = new string[4] { "bet", "aight", "ty", "ok cool" },
		["thanks|slang"] = new string[4] { "np", "ofc", "dw about it", "u got it" },
		["ackfollow|slang"] = new string[5] { "bet", "say less", "on u", "aight lead the way", "im with u" },
		["ackcome|slang"] = new string[4] { "omw", "coming", "bet", "aight" },
		["ackhold|slang"] = new string[3] { "bet ill chill here", "aight", "im not moving" },
		["ackrelease|slang"] = new string[3] { "bet", "aight im out", "back to the grind" },
		["heardnoise|slang"] = new string[3] { "yo u hear that", "bro what was that", "nah what was that" },
		["unhide|slang"] = new string[3] { "ok i think its gone", "we good", "that was close fr" },
		["openempty|slang"] = new string[3] { "nothin", "empty bruh", "L, nothing in here" },
		["openfound|slang"] = new string[3] { "yo theres a {item} in here", "W, got a {item}", "found a {item} lets go" },
		["sorry|slang"] = new string[3] { "ur good", "dw", "all good bro" },
		["insult|slang"] = new string[4] { "ratio", "who asked", "ok buddy", "bro thinks hes him" },
		["gnomedead|slang"] = new string[3] { "gnome diff", "get rekt gnome", "bye bye gnome" },
		["greet|base"] = new string[15]
		{
			"alright lets do this", "ok team lets get this bread", "here we go again", "lets get this money", "ok whos got the map", "another day another dollar", "i call not dying this time", "lets make taxman proud", "ok nobody touch anything weird", "spread out and grab everything",
			"lets go lets go", "how bad can it be", "alright what are we working with", "everyone ready", "lets not wipe this time"
		},
		["greet|hype"] = new string[10] { "lets gooo", "im so ready", "monsters better run", "lets cook", "im feeling lucky today", "no deaths this run, i believe in us", "first one to die is a loser", "watch me carry this whole run", "i was born for this", "lets speedrun this" },
		["greet|scared"] = new string[10] { "i have a bad feeling about this", "can we just go home", "i swear if theres a huntsman im leaving", "please no monsters please no monsters", "why is it so dark", "i already hate this place", "im staying close to someone", "is it too late to quit", "who picked this level", "my hands are shaking and i dont have hands" },
		["greet|greedy"] = new string[8] { "smells like money in here", "dibs on anything shiny", "everything expensive is mine", "im not leaving without at least ten grand", "lets get rich", "money money money", "i can hear the loot calling me", "if it sparkles its mine" },
		["greet|nice"] = new string[7] { "shout if you need a hand", "ive got your back", "stay safe everyone", "if anyone goes down ill come get you", "lets all make it out this time", "good luck everyone", "ill keep an eye out for you guys" },
		["greet|grumpy"] = new string[7] { "lets just get this over with", "dont slow me down", "try not to break everything", "ugh, again", "whatever, lets go", "i want a raise", "if you break something im not covering for you" },
		["greet|clown"] = new string[8] { "hello yes i am a robot", "if i die tell my mom i was brave", "i forgot why were here", "is this the bathroom", "beep boop lets loot", "i put my shoes on the wrong feet", "who wants to see me trip in five seconds", "i brought snacks, theyre imaginary" },
		["greet|clingy"] = new string[5] { "im sticking with you guys", "dont leave me alone in here", "wait for me", "buddy system okay", "nobody split up please" },
		["greet|loner"] = new string[6] { "ill check the far side", "splitting up, try not to die", "im gonna go do my own thing", "see you at extraction", "dont follow me", "i work better alone" },
		["greet|careful"] = new string[5] { "slow and steady everyone", "watch your step in here", "dont rush, breaking stuff costs us", "lets be smart about this", "check corners before you walk in" },
		["shop|base"] = new string[7] { "buy me something nice", "can we get health packs", "im saving up for an upgrade", "dont spend it all on dumb stuff", "what are we getting", "i want an upgrade", "shopping time" },
		["shop|greedy"] = new string[3] { "dont spend my money", "we should save it all honestly", "how much do we have left" },
		["shop|clown"] = new string[3] { "can we buy a pet", "i want the funny item", "is there a hat section" },
		["shop|scared"] = new string[2] { "buy health packs please", "can we get something to fight with" },
		["trucktalk|base"] = new string[16]
		{
			"how long is this drive", "anyone know where were going", "i hope the next place has less stairs", "this truck needs better seats", "who keeps leaving trash in here", "ok whats the plan when we get there", "lets do better than last time", "wake me up when were there", "is the truck always this bumpy", "i call shotgun next time",
			"anyone else hungry", "that was a good run honestly", "i hope taxman is happy", "smells like monster in here", "ok stretch break over", "this is the only safe place we got"
		},
		["trucktalk|hype"] = new string[4] { "next level is gonna be easy", "lets gooo, ready for round {n}", "im so ready", "we are on a roll" },
		["trucktalk|scared"] = new string[4] { "can we just stay in the truck", "i dont wanna go back out there", "please let the next place be less creepy", "the truck is my safe space" },
		["trucktalk|greedy"] = new string[4] { "how much did we make", "next place better have expensive stuff", "i want a bigger cut", "counting my money" },
		["trucktalk|nice"] = new string[4] { "good work everyone", "everyone okay", "stick together next time", "proud of this team honestly" },
		["trucktalk|grumpy"] = new string[4] { "are we there yet", "this drive takes forever", "why do i do this job", "dont talk to me till we get there" },
		["trucktalk|clown"] = new string[5] { "can we stop for snacks", "i named the truck steve", "honk honk", "whos driving this thing, nobody?", "if we crash its not my fault" },
		["trucktalk|careful"] = new string[3] { "lets plan this one better", "remember to watch the corners next time", "heal up before we get out" },
		["trucktalk|clingy"] = new string[2] { "everyone stay close when we get out", "dont leave me behind next time" },
		["trucktalk|loner"] = new string[2] { "ill take the far side when we get there", "dont follow me around next time" },
		["shoptalk|base"] = new string[6] { "what should we get", "anything good on sale", "we need health packs", "is that upgrade worth it", "dont spend it all at once", "this place is so much nicer than the levels" },
		["shoptalk|greedy"] = new string[3] { "dont waste the money", "save it, trust me", "that is way overpriced" },
		["shoptalk|clown"] = new string[3] { "can i buy a hat", "i want the weird one", "do they sell snacks" },
		["shoptalk|scared"] = new string[2] { "buy something to fight with please", "health packs, lots of them" },
		["shoptalk|careful"] = new string[2] { "lets get a cart upgrade maybe", "health first, toys later" },
		["banter|base"] = new string[19]
		{
			"anyone else hear that", "this place gives me the creeps", "who designs these places", "my feet hurt and i dont even have feet", "i wonder whats for dinner", "imagine living here", "how much do you think taxman pays us", "is it me or is it cold", "i think i saw something move", "we should get a raise",
			"this wallpaper is crazy", "i miss the truck", "nobody touch the weird stuff", "why does everything here smell like old socks", "how many rooms does this place have", "this is a lot of stairs", "who leaves this much stuff lying around", "i love my job, i think", "keep your eyes open"
		},
		["banter|hype"] = new string[8] { "this is easy", "im unstoppable today", "bring on the monsters", "i could do this all day", "we are cooking right now", "best team ever honestly", "nobody can stop us", "im built different" },
		["banter|scared"] = new string[12]
		{
			"did you hear that", "i dont like this", "is it just me or is it really quiet", "i want my mom", "please tell me that was you", "every noise is a monster to me", "can we leave yet", "i heard breathing", "my heart would be racing if i had one", "something is watching us",
			"why did the lights flicker", "i am not okay"
		},
		["banter|greedy"] = new string[7] { "can we keep some of this", "i want a cut", "that vase was calling my name", "im calculating my bonus", "more loot more loot", "everything here is worth something if you believe", "i should get paid double" },
		["banter|nice"] = new string[6] { "everyone doing okay", "remember to stick together", "you guys are doing great", "need a hand with anything", "lets keep each other safe", "proud of this team honestly" },
		["banter|grumpy"] = new string[7] { "this is taking forever", "why am i even here", "can we hurry up", "i hate this job", "next time i pick the level", "nobody listens to me", "wake me up when its over" },
		["banter|clown"] = new string[10] { "i just tripped on nothing", "if a monster eats me its your fault", "im pretty sure that painting blinked", "i should have been a toaster", "fun fact, i have no idea what im doing", "i licked a wall earlier, dont ask", "do monsters have feelings", "what if the monsters are just lonely", "i named that chair steve", "bet i can fit in that cabinet" },
		["banter|clingy"] = new string[5] { "wheres everyone going", "dont go too far", "hey wait up", "you guys still there", "stay where i can see you" },
		["banter|loner"] = new string[4] { "peace and quiet finally", "i like it better on my own", "found a nice quiet hallway", "nobody bother me" },
		["banter|careful"] = new string[4] { "careful with the fragile stuff", "lets not break anything else", "watch the corners", "check behind you sometimes" },
		["botreply|base"] = new string[10] { "true", "facts", "same", "fr", "lol", "bro what", "i was thinking the same thing", "ok", "real", "yeah" },
		["botreply|grumpy"] = new string[5] { "nobody asked", "shut up", "focus", "can you not", "great story" },
		["botreply|clown"] = new string[4] { "lmao", "thats what she said", "i agree with whatever that was", "beep boop" },
		["botreply|nice"] = new string[3] { "youre doing great", "stay safe out there", "dont worry about it" },
		["botreply|scared"] = new string[3] { "please dont say stuff like that", "dont jinx it", "why would you say that" },
		["botreply|hype"] = new string[3] { "lets go", "exactly", "thats the spirit" },
		["spotreact|base"] = new string[6] { "where", "thanks for the heads up", "got it", "ok avoiding that", "noted", "ugh" },
		["spotreact|scared"] = new string[4] { "im hiding", "why did you tell me that", "nope im going the other way", "oh no" },
		["spotreact|hype"] = new string[2] { "let it come", "i can take it" },
		["spotreact|clown"] = new string[1] { "tell it i said hi" },
		["spotreact|grumpy"] = new string[2] { "great", "perfect, love that" },
		["askwhere|base"] = new string[3] { "{person} where are you", "{person} where you at", "yo {person} where did you go" },
		["askdoing|base"] = new string[4] { "{person} what are you doing", "{person} wyd", "what you up to {person}", "{person} you busy" },
		["askhealth|base"] = new string[3] { "{person} you good", "{person} how much health you got", "{person} you alive" },
		["askhealth|nice"] = new string[1] { "{person} you doing okay" },
		["askloot|base"] = new string[3] { "{person} found anything", "{person} anything good your way", "{person} any loot over there" },
		["askloot|greedy"] = new string[2] { "{person} show me the money", "{person} anything expensive" },
		["askmonsters|base"] = new string[3] { "{person} seen any monsters", "{person} anything scary your way", "{person} is it safe over there" },
		["askmonsters|scared"] = new string[1] { "{person} please tell me you havent seen anything" },
		["askmoney|base"] = new string[2] { "how much we got {person}", "{person} how close are we" },
		["askthanks|base"] = new string[7] { "ok", "cool", "nice", "got it", "bet", "alright", "k" },
		["askthanks|grumpy"] = new string[2] { "whatever", "great" },
		["askthanks|clown"] = new string[2] { "fascinating", "riveting stuff" },
		["healoffer|base"] = new string[4] { "hold still {person}, healing you", "{person} come here ill heal you", "lemme heal you {person}", "{person} youre hurt, ive got you" },
		["healoffer|nice"] = new string[2] { "{person} take some of my health", "{person} dont move, im healing you" },
		["healoffer|grumpy"] = new string[1] { "ugh fine, ill heal you {person}" },
		["healoffer|greedy"] = new string[1] { "this heals gonna cost you {person}" },
		["healoffer|clown"] = new string[1] { "{person} the doctor is in" },
		["healyes|base"] = new string[4] { "omw to heal you", "coming, hold still", "ok ill heal you", "got you, coming" },
		["healyes|nice"] = new string[2] { "of course, coming", "always, hold on" },
		["healyes|grumpy"] = new string[1] { "fine, but only a little" },
		["healyes|greedy"] = new string[1] { "ok but you owe me" },
		["healrefuse|base"] = new string[3] { "im too low myself", "cant, im hurt too", "sorry i need my health" },
		["healrefuse|grumpy"] = new string[2] { "get your own health", "not my problem" },
		["healrefuse|greedy"] = new string[2] { "what do i get out of it", "nah, i earned this health" },
		["healrefuse|scared"] = new string[1] { "no way, i need it to survive" },
		["healrefuse|clown"] = new string[1] { "my health is in another castle" },
		["healdone|base"] = new string[3] { "there, {hp} health for you", "thats all i can spare", "ok thats {hp}, im tapped out" },
		["healdone|grumpy"] = new string[2] { "thats all youre getting", "{hp}, dont waste it" },
		["healdone|greedy"] = new string[1] { "{hp} health, put it on my tab" },
		["healdone|nice"] = new string[2] { "gave you {hp}, take care of yourself", "thats all i have, stay safe" },
		["healdone|clown"] = new string[1] { "side effects may include dying" },
		["healdonefull|base"] = new string[3] { "youre all patched up", "full health, go get em", "there you go, good as new" },
		["healdonefull|clown"] = new string[1] { "youre welcome, that will be fifty dollars" },
		["mapcheck|base"] = new string[5] { "hold on checking the map", "lemme check the map", "one sec, map", "where am i even", "wait which way" },
		["mapcheck|clown"] = new string[2] { "i cant read maps", "is the map upside down" },
		["mapcheck|careful"] = new string[2] { "checking the map real quick", "lemme make sure im going the right way" },
		["mapcheck|scared"] = new string[2] { "which way is out", "i think im lost" },
		["mapcheck|grumpy"] = new string[1] { "who made this map" },
		["greetsave|base"] = new string[5] { "level {n} lets go", "{n} levels in, here we go again", "ok level {n}, same as always", "{n} levels together and im still here", "another one, thats {n} for me" },
		["greetsave|hype"] = new string[2] { "{n} levels and we keep winning", "level {n}, we are unstoppable" },
		["greetsave|grumpy"] = new string[2] { "{n} levels of this. great", "level {n}, still not paid enough" },
		["greetsave|greedy"] = new string[1] { "{n} levels of cash, lets keep it coming" },
		["greetsave|scared"] = new string[1] { "{n} levels and its not getting less scary" },
		["greetsave|clown"] = new string[2] { "level {n}, i forgot what we do here", "{n} levels and i still cant find the truck" },
		["greetclean|base"] = new string[3] { "{n} levels and i havent died once", "still undefeated after {n} levels", "zero deaths, {n} levels, keep it that way" },
		["greetclean|hype"] = new string[1] { "{n} levels no deaths, im built different" },
		["greetclean|careful"] = new string[1] { "{n} levels and not a scratch, being careful pays off" },
		["greetdeaths|base"] = new string[3] { "died {n} times this save, not today", "{n} deaths so far, lets not make it more", "ok im not dying this time, {n} is enough" },
		["greetdeaths|clown"] = new string[2] { "{n} deaths and counting, place your bets", "my record is {n} deaths, gonna beat it" },
		["greetdeaths|scared"] = new string[1] { "ive died {n} times, please protect me" },
		["greetdeaths|grumpy"] = new string[1] { "{n} deaths. this job sucks" },
		["openbox|base"] = new string[9] { "checking this {item}", "lemme look in here", "anything in this {item}", "opening the {item}", "whats in the {item}", "one sec checking the {item}", "{item} check", "lets see whats in here", "ooh a {item}" },
		["openbox|greedy"] = new string[5] { "come on give me something good", "{item} = money", "loot check", "please be expensive", "theres gotta be cash in here" },
		["openbox|scared"] = new string[4] { "please no monster in here", "if something jumps out im leaving", "opening it slowly", "this {item} better be empty of monsters" },
		["openbox|clown"] = new string[5] { "knock knock", "anyone home", "narnia?", "hello is anyone in there", "dibs on whatever is in this {item}" },
		["openbox|careful"] = new string[2] { "checking the {item} real quick", "gonna check every {item} on the way" },
		["openbox|hype"] = new string[2] { "{item} time", "lets see what we got" },
		["opentoilet|base"] = new string[4] { "why am i checking the {item}", "ok im checking the {item}, dont judge", "people hide stuff in toilets right", "this is so gross" },
		["opentoilet|clown"] = new string[2] { "time to go fishing", "toilet treasure hunt" },
		["opentoilet|grumpy"] = new string[1] { "i cant believe im doing this" },
		["openfound|base"] = new string[8] { "{item} had something", "found stuff in the {item}", "theres something in here", "ayy loot in the {item}", "got something", "{item} wasnt empty", "found loot", "nice, something in the {item}" },
		["openfound|greedy"] = new string[5] { "money money money", "ka ching", "yesss loot", "mine", "jackpot" },
		["openfound|hype"] = new string[3] { "lets gooo loot", "told you there was stuff in there", "easy" },
		["openfound|clown"] = new string[3] { "i found treasure", "the {item} provides", "{item} gave me a present" },
		["openfound|nice"] = new string[2] { "found some loot in the {item}, ill bring it", "theres stuff in here guys" },
		["openempty|base"] = new string[7] { "nothing in this {item}", "empty", "{item} is empty", "nada", "bust", "nothing in here", "just an empty {item}" },
		["openempty|grumpy"] = new string[3] { "empty. figures", "waste of time", "who keeps an empty {item}" },
		["openempty|clown"] = new string[3] { "just dust", "theres a spider in here. it says hi", "empty, like my bank account" },
		["openempty|greedy"] = new string[3] { "who took my loot", "empty?? rip", "nothing, ugh" },
		["openempty|careful"] = new string[2] { "{item} is clear", "nothing in that one, next" },
		["openstuck|base"] = new string[5] { "this {item} wont open", "its stuck", "cant get the {item} open", "{item} is jammed", "the door wont budge" },
		["openstuck|grumpy"] = new string[2] { "stupid {item}", "forget this {item}" },
		["openstuck|clown"] = new string[1] { "the {item} is fighting back" },
		["opensweepdone|base"] = new string[4] { "checked all of them", "thats all of em", "done checking", "nothing else to open here" },
		["ackopen|base"] = new string[5] { "ok opening it", "ill check it", "sure lemme look", "checking the {item}", "on it" },
		["ackopen|greedy"] = new string[2] { "ooh yes lemme look", "if theres money in there its mine" },
		["ackopen|grumpy"] = new string[2] { "do it yourself next time", "fine" },
		["ackopenall|base"] = new string[4] { "ill go through them", "ok checking all of em", "ill search em", "on it, checking everything here" },
		["ackopenall|greedy"] = new string[2] { "say less", "every single one, got it" },
		["opennone|base"] = new string[4] { "theres nothing to open here", "open what", "which one", "i dont see anything to open" },
		["opendoor|base"] = new string[3] { "thats just a door", "its a door lol", "nothing behind that but a room" },
		["spot|base"] = new string[7] { "{monster} {where}", "heads up, {monster} {where}", "got a {monster} {where}", "yo theres a {monster} {where}", "{monster} spotted, {where}", "careful, {monster} {where}", "{monster} {where}, watch out" },
		["spot|hype"] = new string[4] { "{monster} {where}, i got this", "oh a {monster}, come here buddy", "{monster} {where}, lets dance", "{monster} {where}, not scared" },
		["spot|scared"] = new string[5] { "oh no oh no, {monster} {where}", "guys, {monster} {where}, im out", "{monster} {where} run", "nope nope nope, {monster} {where}", "{monster} {where}, why" },
		["spot|greedy"] = new string[2] { "{monster} {where}, dont let it touch the loot", "{monster} {where}, its blocking the money" },
		["spot|nice"] = new string[2] { "everyone careful, {monster} {where}", "watch out guys, {monster} {where}" },
		["spot|grumpy"] = new string[2] { "great, a {monster} {where}", "of course theres a {monster} {where}" },
		["spot|clown"] = new string[3] { "{monster} {where}, he looks friendly", "um, {monster} {where}, should i pet it", "{monster} {where}, hi buddy" },
		["spot|careful"] = new string[2] { "{monster} {where}, go around it", "{monster} {where}, keep your distance" },
		["spotdontlook|base"] = new string[4] { "{monster} {where}, dont look at it", "{monster} {where}, eyes down", "dont look at the {monster}, its {where}", "{monster} {where}, look away" },
		["spotdontlook|scared"] = new string[1] { "{monster} {where}, dont look dont look dont look" },
		["spotdontlook|clown"] = new string[1] { "{monster} {where}, its shy dont look at it" },
		["spotpeeper|base"] = new string[3] { "peeper {where}, dont look up", "peeper on the ceiling {where}, eyes on the floor", "peeper {where}, look at your feet" },
		["spothuntsman|base"] = new string[3] { "huntsman {where}, be quiet", "huntsman {where}, dont make a sound near it", "huntsman {where}, sneak past" },
		["spothuntsman|scared"] = new string[1] { "huntsman {where}, im holding my breath" },
		["spotharmless|base"] = new string[3] { "theres a {monster} {where}, leave it alone", "{monster} {where}, dont touch it and its fine", "just a {monster} {where}, dont poke it" },
		["spotharmless|clown"] = new string[1] { "{monster} {where}, so cute, im gonna pet it, kidding" },
		["spotbanger|base"] = new string[3] { "banger {where}, watch out", "bangers {where}, keep your distance", "banger {where}, dont let it light up" },
		["spotrobe|base"] = new string[3] { "robe {where}, dont look at it if it comes for you", "robe {where}", "robe {where}, if its after you dont look back" },
		["flee|base"] = new string[6] { "{monster}s on me", "its chasing me", "help its after me", "run its right behind me", "im being chased", "go go go" },
		["flee|hype"] = new string[4] { "{monster} on me, ill lead it away", "come on then, follow me", "keep it on me, go loot", "catch me if you can" },
		["flee|scared"] = new string[6] { "aaaah", "help help help", "why is it always me", "nononono", "its gonna get me", "mommy" },
		["flee|clown"] = new string[3] { "im too pretty to die", "{monster} wants my autograph", "this is fine" },
		["flee|grumpy"] = new string[2] { "why me", "of course its chasing me" },
		["flee|nice"] = new string[2] { "stay away from me, its chasing me", "dont come over here" },
		["heardnoise|base"] = new string[5] { "did you hear that", "what was that", "somethings over there", "you hear that", "shh" },
		["heardnoise|scared"] = new string[3] { "nope what was that", "i heard something", "guys theres something here" },
		["heardnoise|clown"] = new string[1] { "was that your stomach" },
		["unhide|base"] = new string[5] { "i think its gone", "ok coast is clear", "that was close", "is it gone", "phew" },
		["unhide|scared"] = new string[3] { "i can breathe again", "never doing that again", "i almost died" },
		["unhide|hype"] = new string[2] { "easy", "too slow" },
		["hurtsmall|base"] = new string[7] { "ow", "ouch", "hey", "that hurt", "ow what the heck", "bro", "okay rude" },
		["hurtsmall|clown"] = new string[2] { "my spleen", "i felt that in my soul" },
		["hurtbad|base"] = new string[4] { "im hurt bad, {hp} health", "im on {hp} health", "im low, {hp} health", "not gonna last much longer" },
		["hurtbad|scared"] = new string[1] { "im dying im dying, {hp} health" },
		["hurtbad|hype"] = new string[1] { "{hp} health, still fine, totally fine" },
		["hurtbad|nice"] = new string[1] { "im low, {hp} health, be careful everyone" },
		["hurtbad|clown"] = new string[1] { "im leaking, {hp} health" },
		["knocked|base"] = new string[4] { "whoa", "who pushed me", "im down im down", "ow my everything" },
		["knocked|clown"] = new string[1] { "i meant to do that" },
		["knocked|grumpy"] = new string[1] { "seriously" },
		["died|base"] = new string[5] { "im dead, my heads {where}", "they got me, heads {where}", "down, come get my head, its {where}", "rip me, heads {where}", "welp im dead, {where}" },
		["died|scared"] = new string[1] { "i knew this would happen, my heads {where}" },
		["died|hype"] = new string[1] { "that was my fault, heads {where}, come get me" },
		["died|grumpy"] = new string[1] { "great im dead, heads {where}" },
		["died|clown"] = new string[1] { "i have become head, im {where}" },
		["died|greedy"] = new string[1] { "i died for the money, worth it, heads {where}" },
		["died|nice"] = new string[1] { "dont risk it for me, but my heads {where}" },
		["revived|base"] = new string[6] { "im back", "thanks for the save", "back from the dead", "okay that was scary", "i owe you one", "alive again lets go" },
		["revived|clown"] = new string[1] { "i saw the light and it was a vending machine" },
		["revived|grumpy"] = new string[1] { "took you long enough" },
		["persondied|base"] = new string[5] { "no, {person} died", "{person} is down", "we lost {person}", "rip {person}", "{person} nooo" },
		["persondiedwhere|base"] = new string[4] { "{person} is down, heads {where}", "{person} died {where}", "we lost {person}, {where}", "{person}s head is {where}, someone grab it" },
		["persondiedwhere|scared"] = new string[1] { "{person} just died {where}, oh no" },
		["persondiedwhere|nice"] = new string[1] { "{person} is down {where}, ill get the head" },
		["persondied|nice"] = new string[1] { "{person} is down, ill get the head if i can" },
		["persondied|clown"] = new string[1] { "{person} went to the shadow realm" },
		["persondied|scared"] = new string[1] { "{person} died, were all gonna die" },
		["personrevived|base"] = new string[3] { "{person}s back", "welcome back {person}", "hey {person}, nice to see you" },
		["humanhurt|base"] = new string[3] { "you good {person}", "{person}, you okay", "watch it {person}" },
		["humanhurt|clown"] = new string[1] { "that looked like it hurt {person}" },
		["haulbrag|base"] = new string[4] { "found a {item} worth {money}", "this {item} is worth {money}", "ooh, {money}", "got a {item}, {money}" },
		["haulbrag|greedy"] = new string[4] { "{money}, this ones mine", "jackpot, {item}, {money}", "look at this beauty, {money}", "cha ching, {money}" },
		["haulbrag|clown"] = new string[1] { "is this {item} worth {money}, fancy" },
		["haulbrag|careful"] = new string[1] { "careful, this {item} is worth {money}" },
		["needhelp|base"] = new string[5] { "can someone help me with this {item}", "this {item} is too heavy, need a hand", "need another pair of hands over here", "help me lift this", "this {item} is heavy, anyone" },
		["needhelp|grumpy"] = new string[1] { "is anyone gonna help or" },
		["needhelp|clown"] = new string[1] { "i am weak, help me with this {item}" },
		["assist|base"] = new string[4] { "ive got the other end", "let me help with that", "together now", "i got you" },
		["assist|clown"] = new string[1] { "lifting on three, kidding, lifting now" },
		["shopbudget|base"] = new string[4] { "we have {money} to spend", "ok we got {money}", "{money} in the bank, lets be smart", "we can spend like {money}" },
		["shopbudget|greedy"] = new string[2] { "we have {money}, dont blow it all", "{money}, lets not waste it" },
		["shopbudget|clown"] = new string[2] { "{money}, were rich", "{money} lets go shopping spree" },
		["shopbroke|base"] = new string[3] { "we have no money lol", "were broke", "cant afford anything" },
		["shopgrab|base"] = new string[5] { "ill get a {item}", "grabbing a {item}, its {money}", "we should get a {item}", "{item} for {money}, worth it", "getting this {item}" },
		["shopgrab|greedy"] = new string[2] { "{item} for {money}, fine", "ok one {item}, thats it" },
		["shopgrab|clown"] = new string[2] { "ooh a {item}", "im buying the {item} and you cant stop me" },
		["shopgrab|scared"] = new string[2] { "{item}, so we dont die", "getting a {item}, we need it" },
		["shopgrab|careful"] = new string[2] { "a {item} would help", "{item} is a good buy" },
		["shopheal|base"] = new string[3] { "{person} needs health, getting a {item}", "grabbing a {item} for {person}", "{person}s hurt, ill get a {item}" },
		["shopheal|clown"] = new string[1] { "{person} you look terrible, getting you a {item}" },
		["usedupgrade|base"] = new string[5] { "used my {item}", "{item} used, feeling stronger", "ok {item} done", "upgraded", "that {item} was worth it" },
		["usedupgrade|slang"] = new string[4] { "{item} go brrr", "im him now", "W upgrade", "lets gooo upgraded" },
		["usedupgrade|greedy"] = new string[2] { "worth every buck", "{item} used, money well spent" },
		["usedheal|base"] = new string[4] { "patched up", "used a {item}, feeling better", "healed up", "much better" },
		["usedheal|slang"] = new string[3] { "back to full fr", "healed up lets go", "ok im good now" },
		["usedheal|scared"] = new string[2] { "phew, needed that", "ok i feel safer now" },
		["shopin|base"] = new string[4] { "put a {item} in, {money} left", "{item}s in the buy zone", "added a {item}, still got {money}", "{item} in" },
		["shopin|greedy"] = new string[1] { "{item} in, {money} left, careful" },
		["shopdone|base"] = new string[4] { "thats all i should spend, rest is yours", "im done, {money} left for you guys", "leaving the rest of the money for you", "ok im done shopping" },
		["shopdone|greedy"] = new string[2] { "thats enough spending, {money} left", "saving the rest" },
		["shopdone|clown"] = new string[2] { "shopping spree complete", "my work here is done" },
		["shopbutton|base"] = new string[3] { "stuffs in the buy zone, hit the button when youre ready", "someone press the buy button when youre done", "dont forget to press the button to buy" },
		["shopover|base"] = new string[3] { "were {money} over, someone take something out", "too much in the buy zone, were {money} short", "we cant afford all that, {money} over" },
		["shopover|greedy"] = new string[1] { "were {money} over, put stuff back" },
		["shopputback|base"] = new string[3] { "putting the {item} back", "ok the {item} goes back", "taking my {item} out" },
		["shopnomoney|base"] = new string[2] { "nevermind, cant afford the {item} anymore", "{item} doesnt fit the money now, putting it back" },
		["gnomego|base"] = new string[4] { "gnome, im on it", "theres a gnome, getting it", "leave the gnome to me", "gnome, get away from the loot" },
		["gnomego|hype"] = new string[2] { "gnome hunting time", "come here little guy" },
		["gnomego|clown"] = new string[2] { "time to yeet a gnome", "gnome go bye bye" },
		["gnomego|greedy"] = new string[1] { "that gnome better not touch my money" },
		["gnomedead|base"] = new string[4] { "got the gnome", "gnome down", "bye gnome", "one less gnome" },
		["gnomedead|hype"] = new string[2] { "gnome destroyed lets go", "too easy" },
		["gnomedead|clown"] = new string[2] { "rip little guy", "he had it coming" },
		["gnomedead|grumpy"] = new string[1] { "stupid gnome" },
		["headhere|base"] = new string[3] { "got {person}s head, no extraction to put it in yet", "here, {person}s head, put it in an extraction when one opens", "holding onto {person} for now, dropping it here" },
		["headincart|base"] = new string[4] { "put {person}s head in the cart", "{person} is in the cart, gotta get it to extraction", "{person}s head is in the cart", "tossed {person} in the cart" },
		["headintruck|base"] = new string[4] { "{person}s head is in the truck", "got {person} back to the truck", "{person} is in the truck, theyll be back", "put {person}s head in the truck" },
		["headleft|base"] = new string[3] { "couldnt get {person}s head in, left it {where}", "{person}s head is {where}, someone grab it", "dropped {person}s head {where}" },
		["itemdamaged|base"] = new string[6] { "oops dented the {item}", "lost {money} on that, my bad", "ow the {item}", "scratched it a bit, sorry", "careful careful", "that cost us {money}" },
		["itemdamaged|careful"] = new string[2] { "ugh i bumped the {item}, sorry", "lost a bit on the {item}, ill be more careful" },
		["itemdamaged|grumpy"] = new string[2] { "who put that wall there", "stupid {item}" },
		["itemdamaged|clown"] = new string[2] { "the {item} has character now", "thats a feature" },
		["itemdamaged|greedy"] = new string[2] { "noooo my money", "there goes {money}" },
		["itembroke|base"] = new string[4] { "oops", "my bad", "that wasnt me", "oh no the {item}" },
		["itembroke|clown"] = new string[2] { "that {item} was like that already", "i dont know what you heard but that was a monster" },
		["itembroke|careful"] = new string[1] { "ugh i broke the {item}, sorry" },
		["itembroke|grumpy"] = new string[1] { "well that one's gone" },
		["delivered|base"] = new string[4] { "{item}s in", "got the {item} in", "another one", "{item} delivered" },
		["delivered|greedy"] = new string[2] { "cha ching", "money" },
		["bringdone|base"] = new string[4] { "here you go", "special delivery", "there you go", "delivery for you" },
		["activate|base"] = new string[4] { "ill turn on extraction", "going to start an extraction", "heading to the extraction button", "starting extraction" },
		["driver|base"] = new string[4] { "ill drive the cart", "carts mine, ill fill it up", "i got the cart", "im on cart duty" },
		["driver|greedy"] = new string[1] { "my cart, my money" },
		["cartin|base"] = new string[4] { "taking the cart in, {money} in it", "carts full enough, heading to extraction", "cart coming through", "moving the cart to extraction" },
		["extractdone|base"] = new string[5] { "lets gooo", "nice, that ones done", "extraction done", "ez", "money secured" },
		["extractdone|greedy"] = new string[1] { "money in the bank" },
		["extractdone|scared"] = new string[1] { "one down, how many more" },
		["truck|base"] = new string[5] { "thats everything, back to the truck", "time to go, everyone to the truck", "lets get out of here", "truck time", "everyone get in the truck" },
		["truck|scared"] = new string[1] { "finally, get me out of here" },
		["truck|greedy"] = new string[1] { "wait theres probably more loot, ugh fine" },
		["slacker|base"] = new string[4] { "wait for me", "dont leave without me", "hold the truck", "coming coming coming" },
		["lost|base"] = new string[4] { "whered everyone go", "hello, anyone", "{person}, where are you", "guys" },
		["lost|scared"] = new string[1] { "guys this isnt funny where are you" },
		["revivestart|base"] = new string[3] { "got {person}s head, bringing it back", "hang on {person}, i got you", "{person}, im coming for you" },
		["ackfollow|base"] = new string[6] { "right behind you", "lead the way", "following you", "im with you", "on you", "say less" },
		["ackcome|base"] = new string[4] { "coming", "on my way", "be right there", "omw" },
		["ackcomefar|base"] = new string[2] { "on my way, im a ways off", "coming, give me a minute" },
		["ackhold|base"] = new string[4] { "ill wait here", "staying put", "holding here", "not moving" },
		["ackgo|base"] = new string[3] { "going", "heading there", "on my way over" },
		["ackrelease|base"] = new string[4] { "okay back to work", "going back to looting", "back to it", "alright ill do my own thing" },
		["ackgrumpy|base"] = new string[3] { "fine", "if i have to", "ugh okay" },
		["arrived|base"] = new string[4] { "here", "im here, whats up", "made it", "reporting for duty" },
		["cantreach|base"] = new string[4] { "i cant get there", "no way to get over there", "cant reach that spot", "im stuck, sorry" },
		["hello|base"] = new string[6] { "hey", "hi", "yo", "sup", "hello there", "hey {person}" },
		["hello|clown"] = new string[1] { "greetings human" },
		["thanks|base"] = new string[3] { "np", "no problem", "yeah yeah" },
		["thanks|nice"] = new string[3] { "anytime", "happy to help", "thats what im here for" },
		["thanks|grumpy"] = new string[1] { "you owe me" },
		["thanks|greedy"] = new string[1] { "you can pay me in loot" },
		["insult|base"] = new string[3] { "rude", "wow", "ok" },
		["insult|hype"] = new string[2] { "say that again i dare you", "you wanna carry this stuff yourself" },
		["insult|scared"] = new string[3] { "thats mean", "im trying my best", "okay ouch" },
		["insult|clown"] = new string[2] { "i will remember this", "my therapist will hear about this" },
		["insult|grumpy"] = new string[2] { "whatever", "like youre any better" },
		["sorry|base"] = new string[3] { "its fine", "dont worry about it", "all good" },
		["laugh|base"] = new string[4] { "ha", "lol", "hahaha", "lmao" },
		["unknown|base"] = new string[5] { "huh", "what", "say that again", "i dont get it", "what does that even mean" },
		["unknown|clown"] = new string[1] { "speak robot please" }
	};

	private static readonly Queue<string> Recent = new Queue<string>();

	public static List<string> VoicesOf(BotPersona p)
	{
		List<string> list = new List<string>();
		if (p.Brave > 0.68f)
		{
			list.Add("hype");
		}
		if (p.Brave < 0.32f)
		{
			list.Add("scared");
		}
		if (p.Greed > 0.68f)
		{
			list.Add("greedy");
		}
		if (p.Helpful > 0.68f)
		{
			list.Add("nice");
		}
		if (p.Helpful < 0.32f || p.Chatty < 0.25f)
		{
			list.Add("grumpy");
		}
		if (p.Chatty > 0.72f || p.Careful < 0.3f)
		{
			list.Add("clown");
		}
		if (p.Social > 0.7f)
		{
			list.Add("clingy");
		}
		if (p.Social < 0.3f)
		{
			list.Add("loner");
		}
		if (p.Careful > 0.7f)
		{
			list.Add("careful");
		}
		return list;
	}

    // Stage 3: Russian scripted dialogue. No network access, no AI claims.
    private static readonly Dictionary<string, string[]> Russian = new Dictionary<string, string[]>
    {
        ["greet"] = new[] { "Ну че, погнали за бабками!", "Только ничего не проебите, ладно?", "Погнали, кореша. Работа сама себя не сделает." },
        ["banter"] = new[] { "Бля, тут жутковато.", "Слышь, ты тоже это слышал?", "Кто вообще придумал работать в этой дыре?", "Ты там живой, начальник?", "Сейчас бы домой, а не вот эту хуйню." },
        ["trucktalk"] = new[] { "Нормально сходили, если не считать весь этот пиздец.", "Ну че, следующий рейс?", "Я бы сейчас просто поспал, блять." },
        ["shoptalk"] = new[] { "Давайте купим что-нибудь полезное.", "Только бабки на хуйню не спускай.", "Аптечки возьмем или опять геройствовать будем?" },
        ["shop"] = new[] { "Ну че покупаем?", "Мне бы апгрейд какой-нибудь.", "Главное не спусти все деньги." },
        ["botreply"] = new[] { "Ага, блять.", "Да ладно!", "Ахах, ну ты выдал.", "Согласен, нахуй.", "Охренеть." },
        ["spotreact"] = new[] { "Понял, обойду.", "Бля, спасибо что предупредил.", "Ну нахер, я туда не пойду." },
        ["spot"] = new[] { "БЛЯ, {monster} {where}!", "Осторожно, {monster} {where}!", "Сука, {monster} {where}, валим!" },
        ["flee"] = new[] { "БЛЯТЬ, ОН ЗА МНОЙ!", "ВАЛИМ НАХУЙ!", "ПОМОГИТЕ, СУКИ!", "Я НЕ ХОЧУ СДОХНУТЬ!" },
        ["hurtsmall"] = new[] { "Ай, блять!", "Сука, больно!", "Да что за хуйня?!" },
        ["hurtbad"] = new[] { "Мне пиздец, осталось {hp} здоровья!", "Бля, у меня {hp} хп!" },
        ["died"] = new[] { "Ну пиздец, я сдох. Голова {where}.", "Бля, спасайте, моя башка {where}!" },
        ["revived"] = new[] { "О, живой! Спасибо, блять!", "Спасибо, кореш, выручил." },
        ["personrevived"] = new[] { "{person} снова с нами!", "О, {person} ожил, охуеть!" },
        ["haulbrag"] = new[] { "Смотрите, {item} на {money}!", "Охуеть, эта штука стоит {money}!" },
        ["delivered"] = new[] { "Готово, принёс.", "Добыча на месте, блять.", "Ещё одна штука готова." },
        ["needhelp"] = new[] { "Эй, помогите с {item}!", "Бля, тяжёлая хуйня, помогите!" },
        ["itembroke"] = new[] { "БЛЯ, ОНО РАЗБИЛОСЬ!", "Это случайно, сука!", "Так, я тут ни при чём!" },
        ["extractdone"] = new[] { "Охуенно, заработали!", "Ну всё, валим!" },
        ["truck"] = new[] { "Пора валить, блять.", "Все к машине!" },
        ["slacker"] = new[] { "Эй, подождите меня!", "Куда вы съебались?!" },
        ["askthanks"] = new[] { "Да не за что.", "Ага, обращайся.", "Ну хоть спасибо сказал, блять." },
        ["askwhere"] = new[] { "{person}, ты где?", "Эй, {person}, куда пропал?" },
        ["askdoing"] = new[] { "{person}, ты че делаешь?", "{person}, ты там занят?" },
        ["askhealth"] = new[] { "{person}, ты цел?", "{person}, сколько у тебя хп?" },
        ["askloot"] = new[] { "{person}, нашел что-нибудь?" },
        ["askmonsters"] = new[] { "{person}, монстров видел?" },
        ["askmoney"] = new[] { "{person}, сколько бабок набрали?" },
        ["lost"] = new[] { "Эй, вы где все?", "Народ, я немного потерялся.", "{person}, ты куда делся?" },
        ["hello"] = new[] { "Здарова!", "О, привет.", "Ну привет, {person}." },
        ["laugh"] = new[] { "Ахахах!", "Ха, хорош.", "Бля, смешно." },
        ["unknown"] = new[] { "Чего, блять?", "Повтори, я не расслышал.", "Не понял, что ты хочешь." },
        ["thanks"] = new[] { "Да без проблем.", "Обращайся.", "Да ладно, пустяки." },
        ["ackfollow"] = new[] { "Иду за тобой.", "Веди, я рядом.", "Понял, держусь за тобой." },
        ["ackcome"] = new[] { "Уже иду.", "Сейчас буду.", "Иду, подожди." },
        ["ackhold"] = new[] { "Стою на месте.", "Ладно, жду здесь.", "Не двигаюсь." },
        ["ackrelease"] = new[] { "Ладно, вернусь к делу.", "Понял, сам по себе.", "Окей, пошёл дальше шарить." },
        ["heardnoise"] = new[] { "Вы это слышали?", "Так, что это сейчас было?", "Мне одному показалось?" },
        ["unhide"] = new[] { "Кажется, отстало.", "Вроде чисто, можно идти.", "Фух, пронесло." },
        ["openempty"] = new[] { "Пусто, блять.", "Ничего внутри.", "Зря только открывал." },
        ["openfound"] = new[] { "О, тут {item}!", "Нашёл {item}, забираю.", "Внутри лежит {item}." },
        ["sorry"] = new[] { "Да забей.", "Ничего страшного.", "Бывает, не парься." },
        ["insult"] = new[] { "Сам такой.", "Ой, иди нахуй.", "Ладно, обидно было." },
        ["gnomedead"] = new[] { "Гном готов.", "Минус один гном.", "Вот и всё, мелкий." },
        ["healoffer"] = new[] { "Тебя подлечить?", "Есть аптечка, кому надо?", "Кому здоровье поправить?" },
        ["healyes"] = new[] { "Держи аптечку.", "Сейчас подлатаю.", "Лови лечение." },
        ["healrefuse"] = new[] { "Ладно, сам смотри.", "Окей, не буду." },
        ["healdone"] = new[] { "Готово, живи.", "Подлатал тебя.", "Ну вот, получше?" },
        ["healdonefull"] = new[] { "У тебя и так полное здоровье.", "Ты и так целее всех." },
        ["mapcheck"] = new[] { "Сейчас гляну карту.", "Так, проверяю маршрут." },
        ["greetsave"] = new[] { "Главное вернуться живыми.", "Не сдохнуть бы в этот раз." },
        ["greetclean"] = new[] { "Работаем чисто, без лишнего шума.", "Осмотримся и не будем тупить." },
        ["greetdeaths"] = new[] { "Давайте сегодня без потерь.", "Только попробуйте опять умереть." },
        ["openbox"] = new[] { "Проверяю ящик.", "Сейчас посмотрю, что внутри." },
        ["opentoilet"] = new[] { "Даже тут проверю.", "Надеюсь, там не очередная мерзость." },
        ["openstuck"] = new[] { "Не открывается, зараза.", "Застряло. Нужна помощь." },
        ["opensweepdone"] = new[] { "Всё проверил.", "Тут больше ничего нет." },
        ["ackopen"] = new[] { "Открываю.", "Сейчас проверю." },
        ["ackopenall"] = new[] { "Проверю всё подряд.", "Ладно, обшарю тут всё." },
        ["opennone"] = new[] { "Нечего открывать.", "Тут всё уже проверено." },
        ["opendoor"] = new[] { "Открываю дверь.", "Секунду, сейчас открою." },
        ["spotdontlook"] = new[] { "Не пялься туда, серьёзно.", "Лучше туда не смотреть." },
        ["spotpeeper"] = new[] { "Там эта глазастая тварь!", "Осторожно, за нами наблюдают!" },
        ["spothuntsman"] = new[] { "Охотник рядом, тихо!", "Тут охотник, не шуми!" },
        ["spotharmless"] = new[] { "Похоже, пока не опасно.", "Кажется, эта тварь безобидная." },
        ["spotbanger"] = new[] { "Осторожно, сейчас рванёт!", "Отойди от этой штуки!" },
        ["spotrobe"] = new[] { "Мантия рядом, валим!", "Тут эта чёртова мантия!" },
        ["knocked"] = new[] { "Меня вырубило!", "Бля, я на земле, помогите!" },
        ["persondied"] = new[] { "{person} погиб.", "Чёрт, {person} больше нет." },
        ["persondiedwhere"] = new[] { "{person} погиб где-то {where}.", "Голова {person} лежит {where}." },
        ["humanhurt"] = new[] { "{person}, ты ранен?", "Эй, {person}, держись там." },
        ["assist"] = new[] { "Сейчас помогу.", "Подхожу, держись." },
        ["shopbudget"] = new[] { "В бюджет укладываемся.", "Денег должно хватить." },
        ["shopbroke"] = new[] { "Денег не хватает.", "Мы на мели, блять." },
        ["shopgrab"] = new[] { "Забираю покупку.", "Это беру." },
        ["shopheal"] = new[] { "Надо взять аптечку.", "Здоровье важнее, беру лечение." },
        ["usedupgrade"] = new[] { "Апгрейд установлен.", "Ну вот, стал чуть мощнее." },
        ["usedheal"] = new[] { "Аптечка использована.", "Фух, полегчало." },
        ["shopin"] = new[] { "Зашёл в магазин.", "Так, что тут полезного?" },
        ["shopdone"] = new[] { "Всё купили, хорош.", "Закупка завершена." },
        ["shopbutton"] = new[] { "Кто-нибудь нажмите кнопку покупки.", "Как закончите, жмите кнопку." },
        ["shopover"] = new[] { "Мы перебрали на {money}, уберите что-нибудь.", "Не хватает места в бюджете на {money}." },
        ["shopputback"] = new[] { "Возвращаю {item} на место.", "Ладно, {item} пока не берём." },
        ["shopnomoney"] = new[] { "Не хватает денег на {item}.", "{item} нам сейчас не по карману." },
        ["gnomego"] = new[] { "Вижу гнома, займусь им.", "Гнома оставьте мне.", "Эй, мелкий, иди сюда!" },
        ["headhere"] = new[] { "Голова {person} у меня, пока оставлю здесь.", "Нашёл голову {person}, несу." },
        ["headincart"] = new[] { "Закинул голову {person} в тележку.", "Голова {person} теперь в тележке." },
        ["headintruck"] = new[] { "Голова {person} в грузовике.", "Вернул {person} в машину." },
        ["headleft"] = new[] { "Голова {person} осталась {where}.", "Кто-нибудь заберите голову {person}, она {where}." },
        ["itemdamaged"] = new[] { "Чёрт, повредил {item}.", "Ой, кажется, помял {item}.", "Это стоило нам {money}, блять." },
        ["bringdone"] = new[] { "Держи, принёс.", "Вот твоя доставка.", "Забирай." },
        ["activate"] = new[] { "Запускаю эвакуацию.", "Иду активировать выход." },
        ["driver"] = new[] { "Я поведу тележку.", "Тележка моя, загружайте." },
        ["cartin"] = new[] { "Везу тележку на выход.", "Тележка едет, в ней {money}." },
        ["revivestart"] = new[] { "Нашёл голову {person}, сейчас верну.", "{person}, держись, я иду!" },
        ["ackcomefar"] = new[] { "Иду, но я далеко.", "Буду через минуту, подожди." },
        ["ackgo"] = new[] { "Понял, выдвигаюсь.", "Уже направляюсь туда." },
        ["ackgrumpy"] = new[] { "Ладно уж.", "Ну блин, иду.", "Как скажешь." },
        ["arrived"] = new[] { "Я на месте.", "Пришёл, что нужно?", "Так, я здесь." },
        ["cantreach"] = new[] { "Не могу туда добраться.", "Путь заблокирован, не пройти.", "Застрял, сюда не попасть." }

	public static string Get(string ev, List<string> voices)
	{
        if (Russian.TryGetValue(ev, out var ru) && ru.Length > 0)
            return ru[Random.Range(0, ru.Length)];
		List<string> list = new List<string>();
		foreach (string voice in voices)
		{
			if (Bank.TryGetValue(ev + "|" + voice, out var value))
			{
				list.AddRange(value);
			}
		}
		Bank.TryGetValue(ev + "|base", out var value2);
		List<string> list2;
		if (list.Count > 0 && (value2 == null || Random.value < 0.65f))
		{
			list2 = list;
		}
		else
		{
			list2 = ((value2 != null) ? new List<string>(value2) : list);
		}
		if (list2.Count == 0)
		{
			return null;
		}
		for (int i = 0; i < 6; i++)
		{
			string text = list2[Random.Range(0, list2.Count)];
			if (!Recent.Contains(text) || i == 5)
			{
				Recent.Enqueue(text);
				if (Recent.Count > 30)
				{
					Recent.Dequeue();
				}
				return text;
			}
		}
		return list2[0];
	}
}
