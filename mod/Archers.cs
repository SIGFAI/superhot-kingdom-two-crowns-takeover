// The monarch's archers: hired with coins, they follow the player like Kingdom's subjects and loose arrows at the Greed.
// Arrows fly in game time, so SUPERHOT's frozen time leaves them hanging in the air with their glitter trail.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Archer : MonoBehaviour
{
    public int slot;
    public GameObject model;
    public float cooldown = 1f;
    float phase;
    Vector3 last;
    PejAiController target;
    public static readonly Vector2[] Slots = { new Vector2(-4.2f, 5.4f), new Vector2(4.2f, 5.4f), new Vector2(-6.2f, 9.0f), new Vector2(6.2f, 9.0f) };

    void Start() { last = transform.position; phase = Random.value * 6f; }

    Vector3 Home()
    {
        var f = G.Cam != null ? G.Cam.transform.forward : Vector3.forward; f.y = 0f; f.Normalize();
        var r = Vector3.Cross(Vector3.up, f);
        var s = Slots[slot % Slots.Length];
        // in a narrow alley the slot is inside a wall: pull the archer in until the player can see them
        for (float k = 1f; k > 0.1f; k -= 0.25f)
        {
            var p = G.Pos + r * s.x * k + f * s.y;
            if (!Physics.Linecast(G.Pos, p + Vector3.up * 0.2f, out var hit, ~0, QueryTriggerInteraction.Ignore) || !Coins.IsLevel(hit.collider)) return p;
        }
        return G.Pos + r * s.x * 0.2f + f * s.y;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        var home = Home();
        var p = transform.position;
        float dist = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(home.x, 0, home.z));
        if (dist > 14f) p = home;
        p = Vector3.Lerp(p, home, Mathf.Clamp01(dt * 3.5f));
        float gy = Coins.GroundNear(p, G.Pos.y - 1.55f);
        p.y = Mathf.Lerp(p.y, gy, Mathf.Clamp01(dt * 12f));
        float speed = dt > 1e-5f ? (p - last).magnitude / dt : 0f;
        last = p;
        phase += dt * (2.5f + Mathf.Min(speed, 6f) * 1.6f);
        float bob = Mathf.Abs(Mathf.Sin(phase)) * (speed > 0.4f ? 0.12f : 0.025f);
        transform.position = p;

        cooldown -= dt;
        if (target == null || target.IsDead || cooldown < 0.4f) target = Arrows.PickTarget(p + Vector3.up * 1.4f);
        Vector3 look = target != null ? target.transform.position - p : (G.Cam != null ? G.Cam.transform.forward : Vector3.forward);
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            model.transform.rotation = Quaternion.Slerp(model.transform.rotation, Quaternion.LookRotation(look), Mathf.Clamp01(dt * 8f));
        model.transform.position = p + Vector3.up * bob;
        if (cooldown <= 0f)
        {
            cooldown = Random.Range(1.5f, 2.3f);
            if (target != null) Arrows.Fire(p + Vector3.up * 1.35f + model.transform.forward * 0.4f, target);
        }
    }

    void OnDestroy() { if (model != null) Destroy(model); }
}

public static class Archers
{
    public const int Max = 4;
    public static readonly List<Archer> List = new List<Archer>();
    public static int Count => List.Count;
    public const float Yaw = 0f;

    public static Archer Hire(bool announce = true)
    {
        if (List.Count >= Max) return null;
        var f0 = G.Cam != null ? G.Cam.transform.forward : Vector3.forward; f0.y = 0f; f0.Normalize();
        var pos = G.Pos + f0 * 6f + Vector3.Cross(Vector3.up, f0) * (Archer.Slots[List.Count % Archer.Slots.Length].x);
        pos.y = Coins.GroundY(pos, G.Pos.y - 1.55f);
        var go = new GameObject("KArcher");
        go.transform.position = pos;
        var a = go.AddComponent<Archer>();
        a.slot = List.Count;
        a.model = Meshes.Spawn(MeshData_Archer.B64, "tex_archer.png", 1.8f, pos, 0f, box: false, name: "KArcherModel");
        List.Add(a);
        Mix.Burst(a.transform.position + Vector3.up, new Color(0.4f, 0.85f, 0.35f), 14, 3f, 0.09f, 1.2f);
        Mix.Flash(pos + Vector3.up, new Color(1f, 0.85f, 0.4f), 8f, 3f, seconds: 0.5f);
        if (announce) Mix.Say("A NEW ARCHER JOINS YOUR KINGDOM!", 2.2f, new Color(0.6f, 1f, 0.5f), 0.82f, 42);
        return a;
    }
}

public class Arrow : MonoBehaviour
{
    public PejAiController target;
    public Vector3 vel;
    float age, trail;
    bool stuck;
    public Vector3 aim => target != null && !target.IsDead ? target.transform.position + Vector3.up * 1.2f : transform.position + vel;

    void Update()
    {
        float dt = Time.deltaTime;
        if (stuck) { age += dt; if (age > 1.5f) Destroy(gameObject); return; }
        age += dt;
        if (age > 4f) { Destroy(gameObject); return; }
        var p = transform.position;
        if (target != null && !target.IsDead)
        {
            var want = (aim - p).normalized * 30f;
            vel = Vector3.Lerp(vel, want, Mathf.Clamp01(dt * 5f));
        }
        var step = vel * dt;
        if (step.sqrMagnitude > 1e-9f && Physics.Raycast(p, step.normalized, out var h, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore))
        {
            var e = h.collider.GetComponentInParent<PejAiController>();
            if (e != null) { Arrows.Hit(e, h.point, vel.normalized); Destroy(gameObject); return; }
            if (Coins.IsLevel(h.collider)) { transform.position = h.point; stuck = true; age = 0f; return; }
        }
        if (target != null && !target.IsDead && Vector3.Distance(p + step, aim) < 0.7f)
        {
            Arrows.Hit(target, aim, vel.normalized); Destroy(gameObject); return;
        }
        transform.position = p + step;
        if (vel.sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(vel);
        trail += dt;
        if (trail > 0.035f)
        {
            trail = 0f;
            var s = Mix.Shape(PrimitiveType.Cube, p, Vector3.one * 0.11f, new Color(1f, 0.85f, 0.35f), false, "KSpark");
            s.AddComponent<Despawn>().at = Time.unscaledTime + 2.5f;
        }
    }
}

public static class Arrows
{
    public static PejAiController PickTarget(Vector3 from)
    {
        PejAiController best = null; float bd = 38f;
        foreach (var e in G.Enemies())
        {
            var c = e.transform.position + Vector3.up * 1.2f;
            float d = Vector3.Distance(from, c);
            if (d >= bd) continue;
            if (Physics.Linecast(from, c, out var h, ~0, QueryTriggerInteraction.Ignore) && h.collider.GetComponentInParent<PejAiController>() != e && Coins.IsLevel(h.collider)) continue;
            best = e; bd = d;
        }
        return best;
    }

    public static Arrow Fire(Vector3 from, PejAiController target)
    {
        var root = new GameObject("KArrow");
        root.transform.position = from;
        var dir = (target.transform.position + Vector3.up * 1.2f - from).normalized;
        root.transform.rotation = Quaternion.LookRotation(dir);
        Part(root, new Vector3(0.08f, 0.08f, 1.3f), Vector3.zero, new Color(0.62f, 0.4f, 0.18f));
        Part(root, new Vector3(0.16f, 0.16f, 0.26f), Vector3.forward * 0.72f, new Color(0.9f, 0.93f, 1f));
        Part(root, new Vector3(0.12f, 0.12f, 2.2f), Vector3.back * 1.8f, new Color(1f, 0.82f, 0.3f));   // golden streak behind the arrow
        Mix.Glow(from, new Color(1f, 0.8f, 0.3f), 5f, 2.2f, root.transform);
        Part(root, new Vector3(0.13f, 0.015f, 0.18f), Vector3.back * 0.6f, new Color(1f, 1f, 1f));
        Part(root, new Vector3(0.015f, 0.13f, 0.18f), Vector3.back * 0.6f, new Color(0.9f, 0.25f, 0.2f));
        var a = root.AddComponent<Arrow>();
        a.target = target; a.vel = dir * 30f;
        Mix.Play(Mix.Sound("arrow.wav"), from, 0.5f, Random.Range(0.95f, 1.15f));
        Mix.Burst(from, new Color(1f, 0.9f, 0.5f), 4, 2f, 0.06f, 0.6f);
        return a;
    }

    static void Part(GameObject root, Vector3 scale, Vector3 localPos, Color c)
    {
        var s = Mix.Shape(PrimitiveType.Cube, root.transform.position, scale, c, false, "KArrowPart");
        s.transform.SetParent(root.transform, false);
        s.transform.localPosition = localPos;
        s.transform.localRotation = Quaternion.identity;
        s.transform.localScale = scale;
    }

    /// <summary>Two arrows kill a Greed: the first one staggers it (flash, knockback), the second drops it.</summary>
    public static void Hit(PejAiController e, Vector3 at, Vector3 dir)
    {
        if (e == null || e.IsDead) return;
        var skin = e.GetComponent<GreedSkin>();
        Mix.Burst(at, new Color(1f, 0.85f, 0.3f), 14, 6f, 0.09f, 1f);
        Mix.Burst(at, new Color(0.6f, 0.25f, 0.85f), 8, 4f, 0.12f, 1f);
        Mix.Flash(at, new Color(1f, 0.8f, 0.3f), 6f, 4f, seconds: 0.25f);
        Mix.Play(Mix.Sound("greed.wav"), at, 0.5f, 1.4f);
        if (skin != null && skin.hits == 0)
        {
            skin.hits = 1; skin.Flash();
            try { PejAiManager.CURRENT.StunEnemy(e, 6); } catch { }
            e.transform.position += new Vector3(dir.x, 0f, dir.z) * 0.15f;
            return;
        }
        G.Shatter(e);
        G.Shake(0.35f);
    }
}
