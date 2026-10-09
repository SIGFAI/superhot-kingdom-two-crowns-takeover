// Every red crystal man becomes a Greed (a purple masked thief wearing a stolen crown). The game's own enemy keeps
// running underneath: its capsule, AI, bullets and kill logic are untouched, only its look is swapped.
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;

public class GreedSkin : MonoBehaviour
{
    public const float Height = 1.75f;
    public PejAiController ctl;
    public GameObject model, crown;
    public int hits;
    float phase, flash;
    Vector3 last;
    Renderer[] hidden;
    Material mat;

    public static void Dress(PejAiController c)
    {
        if (c == null || c.GetComponent<GreedSkin>() != null) return;
        var s = c.gameObject.AddComponent<GreedSkin>();
        s.ctl = c;
        s.model = Meshes.Spawn(MeshData_Greed.B64, "tex_greed.png", Height, c.transform.position, c.transform.eulerAngles.y, box: false, name: "Greed");
        s.crown = Meshes.Spawn(MeshData_Crown.B64, "tex_crown.png", 0.42f, c.transform.position + Vector3.up * Height, 0f, box: false, name: "GreedCrown");
        s.mat = s.model.GetComponent<Renderer>().material;
        s.last = c.transform.position;
        s.phase = Random.value * 6f;
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in c.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (r is SkinnedMeshRenderer || (m != null && m.shader != null && m.shader.name.Contains("Crystal"))) list.Add(r);
        }
        s.hidden = list.ToArray();
    }

    public void Flash() { flash = 1f; }

    void LateUpdate()
    {
        if (model == null) return;
        foreach (var r in hidden) if (r != null && r.enabled) r.enabled = false;
        var p = transform.position;
        float dt = Time.deltaTime;
        float speed = dt > 1e-5f ? (p - last).magnitude / dt : 0f;
        last = p;
        phase += dt * (3f + Mathf.Min(speed, 5f) * 2.2f);
        float hop = Mathf.Abs(Mathf.Sin(phase)) * (speed > 0.3f ? 0.22f : 0.05f);
        float squash = 1f + Mathf.Sin(phase * 2f) * (speed > 0.3f ? 0.07f : 0.03f);
        float yaw = transform.eulerAngles.y;
        model.transform.SetPositionAndRotation(p + Vector3.up * hop, Quaternion.Euler(0f, yaw + GreedFx.YawOffset, Mathf.Sin(phase) * (speed > 0.3f ? 5f : 1.5f)));
        model.transform.localScale = new Vector3(1f / squash, squash, 1f / squash);
        // never fill the lens: a Greed pressed against the camera is hidden
        bool close = G.Cam != null && Vector3.Distance(G.Cam.transform.position, p + Vector3.up * 0.9f) < 1.3f;
        foreach (var r in model.GetComponentsInChildren<Renderer>()) r.enabled = !close;
        foreach (var r in crown.GetComponentsInChildren<Renderer>()) r.enabled = !close;
        crown.transform.SetPositionAndRotation(p + Vector3.up * (Height * 0.97f * squash + hop) + model.transform.forward * 0.05f,
            Quaternion.Euler(8f, yaw + 20f, Mathf.Sin(phase) * 8f));
        crown.transform.localScale = Vector3.one;
        if (flash > 0f)
        {
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 3f);
            mat.color = Color.Lerp(Color.white, new Color(3f, 2.4f, 2.4f), flash);
        }
    }

    void OnDestroy()
    {
        // killed through any other path (level restart): the model must not stay behind
        if (model != null && !GreedFx.Detached.Contains(model)) { Destroy(model); Destroy(crown); }
    }
}

public static class GreedFx
{
    public static float YawOffset = 0f;
    public static readonly System.Collections.Generic.HashSet<GameObject> Detached = new System.Collections.Generic.HashSet<GameObject>();
    public static int Kills;

    /// <summary>The Greed leaves its body: tumbles through the air with its crown, then fades into coins.</summary>
    public static void Launch(GreedSkin s, Vector3 from)
    {
        if (s == null || s.model == null) return;
        Detached.Add(s.model);
        var m = s.model; var c = s.crown;
        Vector3 away = (m.transform.position - G.Pos); away.y = 0f;
        away = away.sqrMagnitude < 0.01f ? Random.insideUnitSphere : away.normalized;
        Mix.Run(Tumble(m, away * 5f + Vector3.up * 6f, 1.6f, true));
        Mix.Run(Tumble(c, away * 6f + Vector3.up * 8f + Random.insideUnitSphere * 2f, 2.2f, false));
    }

    static System.Collections.IEnumerator Tumble(GameObject go, Vector3 vel, float life, bool big)
    {
        float t = 0f;
        var spin = Random.insideUnitSphere * 540f;
        while (go != null && t < life)
        {
            float dt = Time.deltaTime;
            t += dt;
            vel.y -= 18f * dt;
            var p = go.transform.position + vel * dt;
            float floor = Coins.GroundY(p, go.transform.position.y - 2f);
            if (p.y < floor + 0.2f && vel.y < 0f) { p.y = floor + 0.2f; vel.y *= -0.35f; vel.x *= 0.7f; vel.z *= 0.7f; spin *= 0.6f; }
            go.transform.position = p;
            go.transform.Rotate(spin * dt, Space.World);
            if (big) go.transform.localScale = Vector3.one * Mathf.Clamp01((life - t) / 0.5f + 0.05f);
            yield return null;
        }
        if (go != null) { Detached.Remove(go); Object.Destroy(go); }
    }
}

[HarmonyPatch(typeof(PejAiController), nameof(PejAiController.Kill))]
static class GreedKilled
{
    static void Prefix(PejAiController __instance)
    {
        if (__instance == null || __instance.IsDead) return;
        var p = __instance.transform.position + Vector3.up * 1.2f;
        var skin = __instance.GetComponent<GreedSkin>();
        GreedFx.Launch(skin, p);
        Mix.Play(Mix.Sound("greed.wav"), p, 0.9f, Random.Range(0.9f, 1.15f));
        Mix.Burst(p, new Color(0.55f, 0.2f, 0.8f), 14, 6f, 0.14f, 1.6f);
        Mix.Flash(p, new Color(1f, 0.75f, 0.2f), 9f, 4f, seconds: 0.5f);
        GreedFx.Kills++;
        Coins.DropFor(p, GreedFx.Kills);
    }
}
