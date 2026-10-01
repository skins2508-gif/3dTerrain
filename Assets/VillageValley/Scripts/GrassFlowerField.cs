using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class GrassFlowerField : MonoBehaviour
{
    [Header("Source")]
    public Terrain terrain;
    public Texture2D[] textures = new Texture2D[11];

    [Header("Distribution")]
    [Min(1f)] public float spacing = 3.2f;
    [Range(0f, 1f)] public float minimumGrassLayerWeight = 0.34f;
    [Range(0f, 1f)] public float flowerChance = 0.36f;
    public int randomSeed = 40308;
    public bool clearAroundVillageHouses = true;

    [Header("Appearance")]
    public Vector2 grassHeight = new Vector2(1.2f, 2.0f);
    public Vector2 flowerHeight = new Vector2(1.35f, 2.25f);
    [Range(0f, 2f)] public float windStrength = 0.38f;
    [Range(0f, 5f)] public float windSpeed = 1.25f;
    public Color windColor = new Color(1f, 0.08f, 0.04f, 1f);
    [Range(0f, 1f)] public float windColorStrength = 0.72f;
    [Range(10f, 1000f)] public float maxDrawDistance = 320f;

    private readonly List<Matrix4x4>[] matrices = CreateLists();
    private Material[] materials;
    private Mesh crossedQuad;
    private bool dirty = true;

    private static List<Matrix4x4>[] CreateLists()
    {
        var result = new List<Matrix4x4>[11];
        for (int i = 0; i < result.Length; i++) result[i] = new List<Matrix4x4>();
        return result;
    }

    private void OnEnable() { dirty = true; }
    private void OnValidate() { dirty = true; }

    [ContextMenu("Rebuild Grass Field")]
    public void Rebuild()
    {
        for (int i = 0; i < matrices.Length; i++) matrices[i].Clear();
        if (terrain == null || terrain.terrainData == null) return;

        TerrainData data = terrain.terrainData;
        float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        Vector3 origin = terrain.transform.position;
        var random = new System.Random(randomSeed);
        int columns = Mathf.FloorToInt(data.size.x / spacing);
        int rows = Mathf.FloorToInt(data.size.z / spacing);
        FlowerZone[] flowerZones = FindObjectsByType<FlowerZone>(FindObjectsSortMode.None);
        GrassEraseZone[] eraseZones = FindObjectsByType<GrassEraseZone>(FindObjectsSortMode.None);
        GrassFlowerPaintBrush paintBrush = FindFirstObjectByType<GrassFlowerPaintBrush>();
        if (paintBrush != null && paintBrush.gameObject.scene != gameObject.scene) paintBrush = null;

        for (int z = 0; z <= rows; z++)
        {
            for (int x = 0; x <= columns; x++)
            {
                float jitterX = ((float)random.NextDouble() - 0.5f) * spacing * 0.72f;
                float jitterZ = ((float)random.NextDouble() - 0.5f) * spacing * 0.72f;
                float localX = Mathf.Clamp(x * spacing + jitterX, 0f, data.size.x);
                float localZ = Mathf.Clamp(z * spacing + jitterZ, 0f, data.size.z);
                float u = localX / data.size.x;
                float v = localZ / data.size.z;
                int ax = Mathf.Clamp(Mathf.RoundToInt(u * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt(v * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                if (alpha.GetLength(2) == 0 || alpha[az, ax, 0] < minimumGrassLayerWeight) continue;
                if (clearAroundVillageHouses && NearHouse(localX, localZ)) continue;

                bool flower = random.NextDouble() < flowerChance;
                int type = flower ? 1 + random.Next(10) : 0;
                Vector2 range = flower ? flowerHeight : grassHeight;
                float height = Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
                float width = height * Mathf.Lerp(0.48f, 0.68f, (float)random.NextDouble());
                float y = data.GetInterpolatedHeight(u, v);
                Vector3 worldPosition = origin + new Vector3(localX, y, localZ);
                bool erased = false;
                foreach (GrassEraseZone zone in eraseZones)
                {
                    if (zone.gameObject.scene != gameObject.scene || !zone.isActiveAndEnabled) continue;
                    if (zone.Contains(worldPosition)) { erased = true; break; }
                }
                if (erased) continue;

                int paintedType = 0;
                bool paintedErase = false;
                bool hasPaintStroke = paintBrush != null && paintBrush.TryEvaluate(worldPosition, out paintedErase, out paintedType);
                if (hasPaintStroke && paintedErase) continue;

                Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f);
                foreach (FlowerZone zone in flowerZones)
                {
                    if (zone.gameObject.scene != gameObject.scene || !zone.isActiveAndEnabled || !zone.Contains(worldPosition)) continue;
                    if (random.NextDouble() <= zone.flowerChance) type = Mathf.Clamp(zone.flowerType, 1, 10);
                    break;
                }
                // Manual brush strokes take priority over automatic zones.
                if (hasPaintStroke && paintedType > 0) type = paintedType;
                matrices[type].Add(Matrix4x4.TRS(worldPosition, rotation, new Vector3(width, height, width)));
            }
        }
        BuildResources();
        dirty = false;
    }

    private void LateUpdate()
    {
        if (dirty) Rebuild();
        if (crossedQuad == null || materials == null) BuildResources();
        if (crossedQuad == null || materials == null) return;

        Camera camera = Camera.current != null ? Camera.current : Camera.main;
        for (int type = 0; type < matrices.Length; type++)
        {
            Material material = materials[type];
            if (material == null) continue;
            material.SetFloat("_WindStrength", windStrength);
            material.SetFloat("_WindSpeed", windSpeed);
            material.SetColor("_WindColor", windColor);
            material.SetFloat("_WindColorStrength", windColorStrength);
            material.SetFloat("_MaxDistance", maxDrawDistance);
            material.SetVector("_CameraPosition", camera != null ? camera.transform.position : Vector3.zero);
            List<Matrix4x4> source = matrices[type];
            for (int start = 0; start < source.Count; start += 1023)
            {
                int count = Mathf.Min(1023, source.Count - start);
                var batch = new Matrix4x4[count];
                source.CopyTo(start, batch, 0, count);
                Graphics.DrawMeshInstanced(crossedQuad, 0, material, batch, count, null,
                    ShadowCastingMode.Off, true, gameObject.layer, null);
            }
        }
    }

    private void BuildResources()
    {
        if (crossedQuad == null) crossedQuad = BuildCrossedQuad();
        Shader shader = Shader.Find("VillageValley/GrassFlowerWind");
        if (shader == null) return;
        if (materials == null || materials.Length != 11) materials = new Material[11];
        for (int i = 0; i < materials.Length; i++)
        {
            if (textures == null || i >= textures.Length || textures[i] == null) continue;
            if (materials[i] == null || materials[i].shader != shader)
            {
                materials[i] = new Material(shader) { name = "GrassFlower Runtime " + i, enableInstancing = true };
                materials[i].hideFlags = HideFlags.HideAndDontSave;
            }
            materials[i].mainTexture = textures[i];
        }
    }

    private static Mesh BuildCrossedQuad()
    {
        var mesh = new Mesh { name = "Grass Crossed Quad", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f,0,0), new Vector3(0.5f,0,0), new Vector3(-0.5f,1,0), new Vector3(0.5f,1,0),
            new Vector3(0,0,-0.5f), new Vector3(0,0,0.5f), new Vector3(0,1,-0.5f), new Vector3(0,1,0.5f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one, Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0,2,1, 1,2,3, 1,2,0, 3,2,1, 4,6,5, 5,6,7, 5,6,4, 7,6,5 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static bool NearHouse(float x, float z)
    {
        Vector2[] houses =
        {
            new Vector2(330,180), new Vector2(450,170), new Vector2(590,190), new Vector2(690,225),
            new Vector2(285,295), new Vector2(405,285), new Vector2(535,310), new Vector2(650,330),
            new Vector2(345,410), new Vector2(470,430), new Vector2(595,425), new Vector2(700,465),
            new Vector2(390,525), new Vector2(525,535), new Vector2(635,550)
        };
        Vector2 point = new Vector2(x, z);
        foreach (Vector2 house in houses) if ((point - house).sqrMagnitude < 24f * 24f) return true;
        return false;
    }
}
