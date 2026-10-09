// The land itself: grass and dirt laid over the white floor, pine trees, campfires and treasure chests around the
// room, a dusk glow over the picture and fireflies drifting in the air.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;
using UnityEngine.AI;

public static class Kingdom
{
    static Texture2D dusk;
    static readonly List<Transform> flies = new List<Transform>();
    static readonly List<Vector3> flySeed = new List<Vector3>();
    public static float Dusk = 1f;
    public static Vector3 Centre;

    public static void Dress()
    {
        var c = G.Pos;
        float gy = Coins.GroundY(c, c.y - 1.55f);
        if (NavMesh.SamplePosition(c, out var under, 3.5f, NavMesh.AllAreas)) gy = under.position.y;
        var grass = Mix.Texture("grass.png", filter: false);
        grass.wrapMode = TextureWrapMode.Repeat;
        var root = new GameObject("KingdomLand").transform;
        int rNo = 0, rLvl = 0, rNrm = 0, rY = 0, nav = 0; int tiles = 0, trees = 0, chests = 0, fires = 0;
        var rnd = new System.Random(7);
        for (float gx = -30f; gx <= 30f; gx += 2f)
            for (float gz = -30f; gz <= 30f; gz += 2f)
            {
                var o = c + new Vector3(gx, 0f, gz);
                // the floor: any flat level surface at or below the player's feet, walkable (navmesh) or not
                bool hasHit = Physics.Raycast(new Vector3(o.x, gy + 1.5f, o.z), Vector3.down, out var h, 14f, ~0, QueryTriggerInteraction.Ignore)
                    && Coins.IsLevel(h.collider) && h.normal.y > 0.9f && h.point.y <= gy + 0.9f;
                if (!hasHit) { rNo++; continue; }
                bool walk = NavMesh.SamplePosition(h.point, out var nh, 0.6f, NavMesh.AllAreas);
                bool nearWalk = walk || NavMesh.SamplePosition(h.point, out nh, 2.2f, NavMesh.AllAreas);
                float dp = Vector2.Distance(new Vector2(gx, gz), Vector2.zero);
                bool tile = walk || dp < 16f;
                if (tile)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(q.GetComponent<Collider>());
                    q.name = "KGrass";
                    q.transform.SetParent(root, true);
                    q.transform.position = h.point + Vector3.up * (0.02f + ((int)(gx / 2f + gz / 2f) & 1) * 0.004f);
                    q.transform.rotation = Quaternion.Euler(90f, rnd.Next(4) * 90f, 0f);
                    q.transform.localScale = Vector3.one * 2.06f;
                    var mat = new Material(Shader.Find("Unlit/Texture"));
                    mat.mainTexture = grass; mat.mainTextureScale = new Vector2(1f, 1f);
                    mat.color = new Color(0.92f, 0.85f, 0.8f);
                    q.GetComponent<Renderer>().material = mat;
                    tiles++;
                    // props standing on the floor carve a hole in the navmesh so the Greed walk around them
                    if (dp > 11f && trees < 34 && rnd.NextDouble() < 0.03)
                    {
                        var t = Meshes.Spawn(MeshData_Tree.B64, "tex_tree.png", 3.4f + (float)rnd.NextDouble() * 2f, h.point, (float)rnd.NextDouble() * 360f, true, "KTree");
                        t.transform.SetParent(root, true);
                        Carve(t, 0.6f);
                        trees++;
                    }
                    else if (dp > 8f && fires < 4 && rnd.NextDouble() < 0.012) { Carve(Campfire(h.point, root), 0.7f); fires++; }
                    else if (dp > 7f && chests < 5 && rnd.NextDouble() < 0.012)
                    {
                        var t = Meshes.Spawn(MeshData_Chest.B64, "tex_chest.png", 0.85f, h.point, (float)rnd.NextDouble() * 360f, true, "KChest");
                        t.transform.SetParent(root, true);
                        Carve(t, 0.6f);
                        chests++;
                    }
                }
                else if (dp > 4f && trees < 34 && rnd.NextDouble() < 0.4 && !Physics.Raycast(h.point, Vector3.up, 4.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var t = Meshes.Spawn(MeshData_Tree.B64, "tex_tree.png", 3.2f + (float)rnd.NextDouble() * 2.4f, h.point, (float)rnd.NextDouble() * 360f, true, "KTree");
                    t.transform.SetParent(root, true);
                    trees++;
                }
                else if (nearWalk && dp > 6f && chests < 6 && rnd.NextDouble() < 0.06)
                {
                    var t = Meshes.Spawn(MeshData_Chest.B64, "tex_chest.png", 0.85f, h.point, (float)rnd.NextDouble() * 360f, true, "KChest");
                    t.transform.SetParent(root, true);
                    chests++;
                }
                else if (nearWalk && dp > 8f && fires < 5 && rnd.NextDouble() < 0.05)
                {
                    Campfire(h.point, root);
                    fires++;
                }
            }
        // the town centre: a campfire a few steps in front of the player
        var f = G.Cam.transform.forward; f.y = 0f; f.Normalize();
        var fp = c + f * 6f;
        fp.y = Coins.GroundY(fp, gy);
        Campfire(fp, root);
        Centre = fp;
        Mix.Log("KINGDOM rejects noHit=" + rNo + " notLevel=" + rLvl + " normal=" + rNrm + " y=" + rY + " pos=" + c); Mix.Log("KINGDOM tiles=" + tiles + " trees=" + trees + " chests=" + chests + " fires=" + fires + " groundY=" + gy);
        // fireflies
        for (int i = 0; i < 28; i++)
        {
            var s = Mix.Shape(PrimitiveType.Cube, c, Vector3.one * 0.09f, new Color(1f, 0.9f, 0.4f), false, "KFirefly");
            s.transform.SetParent(root, true);
            flies.Add(s.transform);
            flySeed.Add(new Vector3((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()));
        }
    }

    static void Carve(GameObject go, float radius)
    {
        var o = go.AddComponent<NavMeshObstacle>();
        o.shape = NavMeshObstacleShape.Capsule; o.radius = radius; o.height = 2f; o.carving = true;
    }

    public static GameObject Campfire(Vector3 p, Transform root)
    {
        var go = Meshes.Spawn(MeshData_Campfire.B64, "tex_campfire.png", 1.0f, p, Random.value * 360f, true, "KCampfire");
        if (root != null) go.transform.SetParent(root, true);
        var l = Mix.Glow(p + Vector3.up * 0.9f, new Color(1f, 0.55f, 0.2f), 9f, 2.5f, go.transform);
        go.AddComponent<FlickerLight>().Init(l);
        return go;
    }

    public static void Tick()
    {
        var c = G.Pos;
        float t = Time.unscaledTime;
        for (int i = 0; i < flies.Count; i++)
        {
            if (flies[i] == null) continue;
            var s = flySeed[i];
            var p = c + new Vector3(Mathf.Sin(t * (0.3f + s.x * 0.4f) + s.y * 20f) * (4f + s.z * 12f), 0.5f + s.y * 2.5f + Mathf.Sin(t * 1.3f + s.x * 9f) * 0.3f, Mathf.Cos(t * (0.25f + s.z * 0.4f) + s.x * 20f) * (4f + s.y * 12f));
            flies[i].position = p;
            float b = 0.5f + 0.5f * Mathf.Sin(t * 3f + s.z * 10f);
            flies[i].localScale = Vector3.one * (0.05f + 0.07f * b);
        }
    }

    /// <summary>Warm sunset glow over the top of the picture, violet dusk at the bottom.</summary>
    public static void DrawDusk()
    {
        if (dusk == null)
        {
            dusk = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
            {
                float v = y / 63f;   // 0 bottom, 1 top
                var top = new Color(1f, 0.5f, 0.15f, 0.40f);
                var mid = new Color(1f, 0.7f, 0.4f, 0.08f);
                var bot = new Color(0.35f, 0.12f, 0.5f, 0.38f);
                dusk.SetPixel(0, y, v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bot, mid, v * 2f));
            }
            dusk.Apply();
        }
        var prev = GUI.color;
        GUI.color = new Color(1, 1, 1, Dusk);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), dusk, ScaleMode.StretchToFill);
        // when time stands still the world turns icy blue: the freeze reads at a glance
        float frozen = 1f - Mathf.Clamp01(Time.timeScale / 0.08f);
        if (frozen > 0.01f)
        {
            GUI.color = new Color(0.45f, 0.75f, 1f, 0.26f * frozen);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = prev;
    }
}

public class FlickerLight : MonoBehaviour
{
    Light l; float baseI;
    public void Init(Light light) { l = light; baseI = light.intensity; }
    void Update() { if (l != null) l.intensity = baseI * (0.8f + 0.35f * Mathf.PerlinNoise(Time.unscaledTime * 9f, 3f)); }
}
