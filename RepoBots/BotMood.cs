using System;
using System.Collections.Generic;
using UnityEngine;

namespace RepoBots;

// Stage 2: host-side, script-driven mood system. No external API required.
internal static class BotMood
{
    internal enum State { Normal, Irritated, Breakdown }
    internal sealed class Mood
    {
        internal float Stress;
        internal float LastTick;
        internal float BreakdownUntil;
        internal float LastCommand = -100f;
        internal float LastSupport = -100f;
        internal float LastBreak = -1000f;
        internal int ConsecutiveOrders;
    }
    private static readonly Dictionary<string, Mood> Data = new Dictionary<string, Mood>();
    private static Mood Get(BotBrain b)
    {
        if (!Data.TryGetValue(b.Name, out var m)) { m = new Mood { LastTick = Time.time }; Data[b.Name] = m; }
        float now = Time.time;
        float elapsed = Mathf.Max(0f, now - m.LastTick);
        m.LastTick = now;
        m.Stress = Mathf.Max(0f, m.Stress - elapsed * (IsBolt(b) ? 0.20f : 0.12f));
        return m;
    }
    private static bool IsBolt(BotBrain b) => b.Name.Equals("Bolt", StringComparison.OrdinalIgnoreCase);
    private static float Threshold(BotBrain b) => IsBolt(b) ? 70f : 90f;
    internal static void Reset() => Data.Clear();
    internal static State Current(BotBrain b)
    {
        var m = Get(b);
        if (Time.time < m.BreakdownUntil) return State.Breakdown;
        return m.Stress >= (IsBolt(b) ? 30f : 45f) ? State.Irritated : State.Normal;
    }
    internal static void AddStress(BotBrain b, float amount)
    {
        var m = Get(b);
        m.Stress = Mathf.Clamp(m.Stress + amount * (IsBolt(b) ? 1.2f : 0.85f), 0f, 100f);
        if (m.Stress >= Threshold(b) && Time.time >= m.BreakdownUntil)
        {
            m.BreakdownUntil = Time.time + UnityEngine.Random.Range(IsBolt(b) ? 20f : 60f, IsBolt(b) ? 40f : 100f);
            // Actual object destruction deliberately NOT enabled until game API is verified.
            Plugin.Log.LogInfo((object)("Mood breakdown: " + b.Name));
        }
    }
    internal static bool TrySupport(BotBrain b, string message)
    {
        string s = message.ToLowerInvariant();
        bool supportive = s.Contains("спасибо") || s.Contains("молодец") || s.Contains("отдохни") || s.Contains("извини") || s.Contains("не переживай") || s.Contains("хорошо справ") || s.Contains("не ссы");
        if (!supportive) return false;
        var m = Get(b);
        if (Time.time - m.LastSupport < 45f) return true;
        m.LastSupport = Time.time;
        m.Stress = Mathf.Max(0f, m.Stress - (s.Contains("отдохни") ? 18f : 12f));
        b.SayAck(IsBolt(b) ? "О, нихуя себе, спасибо, братан." : "Спасибо. Хоть кто-то оценил.");
        return true;
    }
    internal static bool RefuseOrder(BotBrain b)
    {
        var m = Get(b);
        bool rapid = Time.time - m.LastCommand < 10f;
        m.ConsecutiveOrders = rapid ? m.ConsecutiveOrders + 1 : 1;
        m.LastCommand = Time.time;
        AddStress(b, rapid ? 12f : 3f);
        State state = Current(b);
        float chance = state == State.Breakdown ? 0.65f : state == State.Irritated ? (IsBolt(b) ? 0.10f : 0.05f) : 0f;
        if (UnityEngine.Random.value >= chance) return false;
        b.SayAck(IsBolt(b) ? "Да заебал ты уже, сам сходи!" : "Блять, хватит. Сейчас сам разбирайся.");
        return true;
    }
}
