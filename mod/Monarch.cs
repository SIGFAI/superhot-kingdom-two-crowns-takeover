// Two crowns call the royal charge: the monarch gallops around the player in slow motion, flinging coins and
// cutting down every Greed in reach.
using System.Collections;
using Sigf.Kit;
using UnityEngine;

public static class Monarch
{
    public static bool Charging;
    public const float YawOffset = 0f;

    public static void Charge()
    {
        if (Charging) return;
        Mix.Run(Run(), "MonarchCharge");
    }

    static IEnumerator Run()
    {
        Charging = true;
        var gold = new Color(1f, 0.85f, 0.3f);
        float prevTime = TimeControl.forcedTimeScale;
        G.ForceTime(0.2f);
        G.Words("TWO;CROWNS");
        Mix.Say("TWO CROWNS! THE MONARCH RIDES!", 3.5f, gold, 0.15f, 60);
        Mix.Play(Mix.Sound("horse.wav"), null, 0.8f);
        Mix.Play(Mix.Sound("fanfare.wav"), null, 0.7f);
        var centre = G.Pos;
        float gy = Coins.GroundNear(centre, centre.y - 1.55f, 1.5f);
        var f = G.Cam.transform.forward; f.y = 0f; f.Normalize();
        var right = Vector3.Cross(Vector3.up, f);
        var m = Meshes.Spawn(MeshData_Monarch.B64, "tex_monarch.png", 2.7f, centre, 0f, box: false, name: "KMonarch");
        Mix.Glow(centre, gold, 14f, 3f, m.transform);
        float t = 0f, killT = 0f, coinT = 0f, dur = 4.2f;
        while (t < dur)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt; killT += dt; coinT += dt;
            centre = G.Pos;
            // two passes across the view: left to right close, then right to left further out
            bool first = t < dur / 2f;
            float u = first ? t / (dur / 2f) : (t - dur / 2f) / (dur / 2f);
            float side = Mathf.Lerp(-13f, 13f, first ? u : 1f - u);
            float depth = first ? 5.5f : 9f;
            var pos = centre + f * depth + right * side;
            pos.y = gy + Mathf.Abs(Mathf.Sin(t * 14f)) * 0.2f;
            var heading = right * (first ? 1f : -1f);
            m.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(heading) * Quaternion.Euler(Mathf.Sin(t * 14f) * 4f, YawOffset, 0f));
            if (t < 0.3f) m.transform.localScale = Vector3.one * (0.2f + t / 0.3f * 0.8f);
            if (t > dur - 0.4f) m.transform.localScale = Vector3.one * Mathf.Max(0.05f, (dur - t) / 0.4f);
            if (coinT > 0.06f) { coinT = 0f; Coins.Spawn(pos + Vector3.up * 1.8f, new Vector3(Random.Range(-2f, 2f), Random.Range(4f, 7f), Random.Range(-2f, 2f)) - heading * 2f); }
            if (killT > 0.3f)
            {
                killT = 0f;
                var e = G.Closest(pos);
                if (e != null && !e.IsDead && Vector3.Distance(e.transform.position, centre) < 40f)
                {
                    Mix.Burst(e.transform.position + Vector3.up, gold, 18, 7f, 0.12f, 1.2f);
                    G.Shatter(e);
                    G.Shake(0.4f);
                }
            }
            yield return null;
        }
        Object.Destroy(m);
        TimeControl.forcedTimeScale = prevTime;
        Charging = false;
    }
}
