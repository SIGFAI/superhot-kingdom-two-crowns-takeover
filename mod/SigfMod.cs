// Kingdom Two Crowns Takeover: SUPERHOT's white rooms become a Kingdom dusk. Every enemy is a crowned Greed that
// spills gold when it falls, coins hire archers who shoot in frozen time, and two crowns call the monarch's charge.
using System.Collections;
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;
using UnityEngine.AI;

public class SigfMod : MixMod
{
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    static bool ready;

    public override void OnLoad() => G.StartLevel = "TheBridge2";

    public override void OnReady()
    {
        ready = true;
        G.CaptureEnemyTemplate();
        // the game's race-to-20-kills would end the level mid-show: make it endless
        foreach (var ec in Object.FindObjectsOfType<EndlessControl>()) ec.raceToKillCount = 1000000;
        Mix.Say("KINGDOM TWO CROWNS", 3.5f, Gold, 0.2f, 80);
        Mix.After(2.5f, () => Mix.Say("the Greed stole the crowns... take them back", 3f, Color.white, 0.3f, 38));
        // the player drops into place for a moment: dress the land once the floor under them is settled
        Mix.After(2.5f, () => { G.Place(G.Pos, G.BestView()); ClearZone(7f); Kingdom.Dress(); Archers.Hire(false); Archers.Hire(false); }, "dress");
        Mix.Every(0.2f, () => { foreach (var e in G.Enemies()) GreedSkin.Dress(e); }, "greedify");
        // in the recorded demo nothing may stand in the lens: an enemy that wanders (or was dropped) next to the player is walked away
        Mix.Every(1.5f, () => { if (Mix.DemoStarted) ClearZone(3.5f); }, "clear");
        Mix.Every(5f, TopUpEnemies, "spawner");
    }

    /// <summary>Greed standing in the player's face at the start are walked away, so the first frame shows the square.</summary>
    static void ClearZone(float radius)
    {
        foreach (var e in G.Enemies())
        {
            if (Vector3.Distance(e.transform.position, G.Pos) > radius) continue;
            for (int i = 0; i < 12; i++)
            {
                var a = Random.value * Mathf.PI * 2f;
                var p = G.Pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(14f, 22f);
                if (!NavMesh.SamplePosition(p, out var nh, 3f, NavMesh.AllAreas)) continue;
                var ag = e.GetComponent<NavMeshAgent>();
                if (ag != null && ag.enabled && ag.Warp(nh.position)) break;
                e.transform.position = nh.position; break;
            }
        }
    }

    static void TopUpEnemies()
    {
        if (Monarch.Charging || G.Enemies().Count >= 5) return;
        SpawnGreed(14f, 22f);
    }

    public static PejAiController SpawnGreed(float minD, float maxD)
    {
        for (int i = 0; i < 12; i++)
        {
            var a = Random.value * Mathf.PI * 2f;
            var p = G.Pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(minD, maxD);
            if (!NavMesh.SamplePosition(p, out var nh, 3f, NavMesh.AllAreas)) continue;
            var e = G.SpawnEnemy(nh.position);
            if (e != null) { GreedSkin.Dress(e); return e; }
        }
        return null;
    }

    public override void OnUpdate()
    {
        if (ready) Kingdom.Tick();
    }

    public override void OnGUI()
    {
        Kingdom.DrawDusk();
        Coins.Draw();
    }

    /// <summary>A Greed in front of the player (the demo's cast), on the navmesh.</summary>
    /// <summary>A Greed around the campfire in the middle of the square, in plain view of the player.</summary>
    static PejAiController GreedAhead(float dist, float side)
    {
        for (int i = 0; i < 30; i++)
        {
            float a = (side * 0.35f + i * 0.9f) ;
            var p = Kingdom.Centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (dist * 0.45f + (i % 5) * 0.9f);
            if (!NavMesh.SamplePosition(p, out var nh, 2f, NavMesh.AllAreas)) continue;
            if (Vector3.Distance(nh.position, G.Pos) < 5f) continue;
            bool crowded = false;
            foreach (var o in G.Enemies()) if (Vector3.Distance(o.transform.position, nh.position) < 1.6f) crowded = true;
            if (crowded) continue;
            if (Physics.Linecast(G.Pos, nh.position + Vector3.up * 1.2f, out var hit, ~0, QueryTriggerInteraction.Ignore) && Coins.IsLevel(hit.collider)) continue;
            var e = G.SpawnEnemy(nh.position);
            if (e != null) { GreedSkin.Dress(e); return e; }
        }
        return null;
    }

    static void Volley()
    {
        foreach (var a in Archers.List) a.cooldown = 0f;
    }

    public override IEnumerator Demo()
    {
        while (Kingdom.Centre == Vector3.zero) yield return null;
        yield return Mix.Wait(0.3f);
        G.ForceTime(0.9f);
        Mix.Run(Sway(), "sway");
        Mix.Say("KINGDOM TWO CROWNS", 3f, Gold, 0.2f, 80);
        GreedAhead(9f, -3f); GreedAhead(11f, 0f); GreedAhead(9f, 3f);
        yield return Mix.Wait(3.2f);
        // moment 1: the archers loose a volley, then time stands still with the arrows in the air
        Volley();
        yield return Mix.Wait(0.2f);
        G.ForceTime(0.04f);
        Mix.Say("TIME STANDS STILL", 3f, Color.white, 0.2f, 70);
        yield return Mix.Wait(2f);
        // moment 2: time creeps back, the arrows land, the Greed stagger and fall in a shower of coins
        G.ForceTime(0.3f);
        Mix.Say("ARROWS LAND", 2f, Gold, 0.2f, 60);
        yield return Mix.Wait(1.5f);
        Volley();
        yield return Mix.Wait(2.5f);
        Volley();
        yield return Mix.Wait(1.5f);
        G.ForceTime(0.9f);
        yield return Mix.Wait(2f);
        // moment 3: the next three, coins pour into the purse, the first crown flies to the player
        GreedAhead(8f, -2f); GreedAhead(10f, 2f); GreedAhead(9f, 0f);
        yield return Mix.Wait(2.5f);
        for (int i = 0; i < 5; i++) { Volley(); yield return Mix.Wait(1.4f); }
        yield return Mix.Wait(2f);
        // moment 4: the third trio gives the second crown: two crowns call the monarch
        GreedAhead(8f, -2f); GreedAhead(10f, 2f); GreedAhead(9f, 0f);
        yield return Mix.Wait(2.5f);
        for (int i = 0; i < 6; i++) { Volley(); yield return Mix.Wait(1.4f); }
        for (int w = 0; w < 3; w++)
        {
            GreedAhead(8f, -2f); GreedAhead(10f, 2f); GreedAhead(9f, 0f);
            yield return Mix.Wait(2f);
            for (int i = 0; i < 4; i++) { Volley(); yield return Mix.Wait(1.4f); }
        }
    }

    /// <summary>The demo player slowly looks left and right across the camp, so the picture always moves.</summary>
    static IEnumerator Sway()
    {
        var f = G.Cam.transform.forward; f.y = 0f;
        float t0 = Time.unscaledTime;
        while (true)
        {
            float a = Mathf.Sin((Time.unscaledTime - t0) * 0.7f) * 14f;
            G.Place(G.Pos, Quaternion.Euler(0f, a, 0f) * f);
            yield return Mix.Wait(0.04f);
        }
    }
}

/// <summary>The game's giant kill-countdown numbers would cover the Kingdom: hide any word with a digit in it.</summary>
[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuick), new[] { typeof(string[]), typeof(bool), typeof(float) })]
static class HideCountdown
{
    static bool Prefix(string[] words)
    {
        if (words == null) return true;
        foreach (var w in words)
            if (!string.IsNullOrEmpty(w)) foreach (char c in w) if (char.IsDigit(c)) return false;
        return true;
    }
}
