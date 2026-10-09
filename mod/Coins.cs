// The kingdom economy: every Greed that falls spills gold coins (they fly in game time, so bullet time freezes them in
// the air), every third one drops a crown, coins buy archers, two crowns call the royal charge. Plus the HUD purse.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Coin : MonoBehaviour
{
    public Vector3 vel;
    public float age;
    public bool landed, magnet;
    float spin;

    void Update()
    {
        var pl = G.Pos + Vector3.down * 0.3f;
        float dt = Time.deltaTime;
        age += dt;
        var p = transform.position;
        float d = Vector3.Distance(p, pl);
        if (!magnet && age > 0.6f && d < Coins.MagnetRange) magnet = true;
        if (magnet)
        {
            // fly into the monarch's purse
            vel = Vector3.Lerp(vel, (pl - p).normalized * 16f, Mathf.Clamp01(dt * 8f));
            transform.position = p + vel * dt;
            if (d < 0.9f) { Coins.Collect(this); return; }
        }
        else if (!landed)
        {
            vel.y -= 16f * dt;
            var np = p + vel * dt;
            Vector3 step = np - p;
            if (step.sqrMagnitude > 1e-8f && Physics.Raycast(p, step.normalized, out var wall, step.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore)
                && Coins.IsLevel(wall.collider))
            {
                vel = Vector3.Reflect(vel, wall.normal) * 0.4f;
                np = p;
            }
            float floor = Coins.GroundY(np, p.y - 1f);
            if (np.y < floor + 0.12f && vel.y < 0f)
            {
                np.y = floor + 0.12f;
                if (vel.y > -2.5f) { landed = true; vel = Vector3.zero; }
                else { vel.y *= -0.45f; vel.x *= 0.7f; vel.z *= 0.7f; Mix.Play(Mix.Sound("coin.wav"), np, 0.2f, Random.Range(1.3f, 1.8f)); }
            }
            transform.position = np;
        }
        // a standing coin spins like in Kingdom; unscaled so a frozen room still glitters
        spin += (landed ? Time.unscaledDeltaTime : dt) * 320f;
        transform.rotation = Quaternion.Euler(0f, spin, 0f);
        if (landed) transform.position = new Vector3(p.x, Coins.GroundY(p, p.y) + 0.2f + Mathf.Sin(Time.unscaledTime * 4f + age) * 0.03f, p.z);
    }
}

public class CrownPickup : MonoBehaviour
{
    float age;
    bool magnet;
    Vector3 vel;

    void Start() { Mix.Glow(transform.position, new Color(1f, 0.8f, 0.25f), 7f, 3f, transform); }

    void Update()
    {
        age += Time.deltaTime;
        var pl = G.Pos;
        var p = transform.position;
        float d = Vector3.Distance(p, pl);
        if (!magnet && age > 0.5f && d < 18f) magnet = true;
        if (magnet)
        {
            vel = Vector3.Lerp(vel, (pl - p).normalized * 14f, Mathf.Clamp01(Time.deltaTime * 6f));
            transform.position = p + vel * Time.deltaTime;
            if (d < 2.4f) { Coins.CollectCrown(this); return; }
        }
        else
        {
            float floor = Coins.GroundY(p, p.y - 2f);
            float want = floor + 1.1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.15f;
            transform.position = new Vector3(p.x, Mathf.Lerp(p.y, want, Mathf.Clamp01(Time.deltaTime * 4f)), p.z);
        }
        transform.rotation = Quaternion.Euler(0f, Time.unscaledTime * 120f, Mathf.Sin(Time.unscaledTime * 2f) * 8f);
    }
}

public static class Coins
{
    public const float MagnetRange = 7f;
    public static int Purse, Crowns, Total;
    public static float PopT, CrownPopT;
    public static int HireCost = 8;
    static float comboT; static int combo;
    static Texture2D coinIcon, crownIcon;

    public static bool IsLevel(Collider c)
    {
        if (c == null || c.isTrigger) return false;
        if (c.GetComponentInParent<PejAiController>() != null || c.GetComponentInParent<PlayerActions>() != null) return false;
        return c.GetComponentInParent<Coin>() == null;
    }

    public static float GroundY(Vector3 p, float fallback)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 0.6f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits) if (IsLevel(h.collider) && h.point.y > best && h.normal.y > 0.5f) best = h.point.y;
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    /// <summary>The floor under p closest to refY (so a unit does not climb onto a crate or hang above a drop).</summary>
    public static float GroundNear(Vector3 p, float refY, float tol = 1.0f)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NaN, bd = tol;
        foreach (var h in hits)
        {
            if (!IsLevel(h.collider) || h.normal.y < 0.5f) continue;
            float d = Mathf.Abs(h.point.y - refY);
            if (d < bd) { bd = d; best = h.point.y; }
        }
        return float.IsNaN(best) ? refY : best;
    }

    public static void Spawn(Vector3 pos, Vector3 vel)
    {
        var root = new GameObject("KCoin");
        root.transform.position = pos;
        var disc = Mix.Shape(PrimitiveType.Cylinder, pos, new Vector3(0.27f, 0.022f, 0.27f), new Color(1f, 0.78f, 0.12f), false, "KCoinDisc");
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var face = Mix.Shape(PrimitiveType.Cylinder, pos, new Vector3(0.21f, 0.028f, 0.21f), new Color(1f, 0.95f, 0.5f), false, "KCoinFace");
        face.transform.SetParent(root.transform, false);
        face.transform.localPosition = Vector3.zero;
        face.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var c = root.AddComponent<Coin>();
        c.vel = vel;
    }

    public static void DropFor(Vector3 p, int killNo)
    {
        int n = Random.Range(5, 9);
        for (int i = 0; i < n; i++)
        {
            var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.6f + 0.5f;
            Spawn(p, dir.normalized * Random.Range(3f, 7.5f) + Vector3.up * 3f);
        }
        Mix.Play(Mix.Sound("coins.wav"), p, 0.7f);
        if (killNo % 3 == 0 && !Monarch.Charging) DropCrown(p + Vector3.up * 0.3f);
    }

    public static void DropCrown(Vector3 p)
    {
        var go = Meshes.Spawn(MeshData_Crown.B64, "tex_crown.png", 0.7f, p, 0f, box: false, name: "KCrown");
        go.AddComponent<CrownPickup>();
        Mix.Burst(p, new Color(1f, 0.85f, 0.3f), 16, 5f, 0.1f, 1.2f);
    }

    public static void Collect(Coin c)
    {
        Purse++; Total++; PopT = 1f;
        if (Time.unscaledTime - comboT > 0.35f) combo = 0;
        comboT = Time.unscaledTime; combo = Mathf.Min(combo + 1, 12);
        Mix.Play(Mix.Sound("coin.wav"), null, 0.35f, 0.9f + combo * 0.06f);
        Object.Destroy(c.gameObject);
        if (Purse >= HireCost && Archers.Count < Archers.Max) { Purse -= HireCost; Archers.Hire(); }
    }

    public static void CollectCrown(CrownPickup c)
    {
        Crowns++; CrownPopT = 1f;
        Mix.Play(Mix.Sound("fanfare.wav"), null, 0.55f, Crowns >= 2 ? 1.1f : 1f);
        Mix.Burst(c.transform.position, new Color(1f, 0.85f, 0.3f), 24, 6f, 0.12f, 1.4f);
        Object.Destroy(c.gameObject);
        G.Shake(0.6f);
        if (Crowns >= 2) { Crowns = 0; Monarch.Charge(); }
        else Mix.Say("ONE CROWN! ONE MORE!", 2f, new Color(1f, 0.85f, 0.3f), 0.8f, 44);
    }

    // ---------- HUD ----------
    static GUIStyle big;
    public static void Draw()
    {
        if (coinIcon == null) { coinIcon = Mix.Texture("coin_icon.png"); crownIcon = Mix.Texture("crown_icon.png"); }
        if (big == null) big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        float k = Screen.height / 1080f;
        PopT = Mathf.Max(0f, PopT - Time.unscaledDeltaTime * 4f);
        CrownPopT = Mathf.Max(0f, CrownPopT - Time.unscaledDeltaTime * 2.5f);
        float s = 1f + PopT * 0.35f;
        var prev = GUI.color;
        GUI.color = new Color(0.12f, 0.06f, 0.02f, 0.78f);
        GUI.DrawTexture(new Rect(24 * k, 24 * k, 330 * k, 96 * k), Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.72f, 0.2f, 1f);
        foreach (var r in new[] { new Rect(24, 24, 330, 4), new Rect(24, 116, 330, 4), new Rect(24, 24, 4, 96), new Rect(350, 24, 4, 96) })
            GUI.DrawTexture(new Rect(r.x * k, r.y * k, r.width * k, r.height * k), Texture2D.whiteTexture);
        GUI.color = Color.white;
        float cs = 72f * s * k;
        GUI.DrawTexture(new Rect(44 * k + (72 * k - cs) / 2f, 72 * k - cs / 2f, cs, cs), coinIcon);
        big.fontSize = Mathf.RoundToInt(58 * s * k);
        Label(new Rect(132 * k, 28 * k, 220 * k, 88 * k), "x " + Purse, new Color(1f, 0.86f, 0.3f));
        // two crown slots: a crown lights up for each one collected, two call the royal charge
        for (int i = 0; i < 2; i++)
        {
            bool have = i < Crowns;
            float cp = (have && i == Crowns - 1) ? 1f + CrownPopT * 0.6f : 1f;
            GUI.color = have ? Color.white : new Color(0.15f, 0.1f, 0.05f, 0.85f);
            float w = 78f * cp * k, h = 65f * cp * k;
            GUI.DrawTexture(new Rect((376 + i * 88) * k, 72 * k - h / 2f, w, h), crownIcon);
        }
        big.fontSize = Mathf.RoundToInt(26 * k);
        Label(new Rect(28 * k, 128 * k, 560 * k, 40 * k), "Archers " + Archers.Count + "/" + Archers.Max + (Archers.Count < Archers.Max ? "   next archer: " + HireCost + " coins" : "   the whole guard is hired"), new Color(0.85f, 0.95f, 0.8f));
        GUI.color = prev;
    }

    static void Label(Rect r, string t, Color c)
    {
        var prev = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), t, big);
        GUI.color = c;
        GUI.Label(r, t, big);
        GUI.color = prev;
    }
}
