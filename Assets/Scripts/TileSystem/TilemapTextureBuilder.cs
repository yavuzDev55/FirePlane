using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapTextureBuilder : MonoBehaviour
{
    [Header("References")]
    public Tilemap groundTilemap;
    public Tilemap missionTilemap;

    // Gruplar
    private Dictionary<BiomeData, List<Vector3Int>> biomeGroups = new();
    private Dictionary<MissionData, List<Vector3Int>> missionGroups = new();

    // Tile cache (biome/mission → CustomTile)
    private Dictionary<BiomeData, CustomTile> biomeTileCache = new();
    private Dictionary<MissionData, CustomTile> missionTileCache = new();

    // Texture boyut bilgisi
    private BoundsInt bounds;
    private int texWidth;
    private int texHeight;

    // Üretilen textureler
    public Texture2D BiomeTexture { get; private set; }
    public Texture2D BiomeIndexTexture { get; private set; }
    private int maxBiomes = 8;

    public Texture2D MissionTexture { get; private set; }

    void Awake()
    {
        bounds = groundTilemap.cellBounds;
        texWidth = bounds.size.x;
        texHeight = bounds.size.y;

        ScanTilemaps();
        BuildBiomeTexture();
        BuildMissionTexture();
    }

    // ——— TARAMA ———

    void ScanTilemaps()
    {
        for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);

                // Biyom grubu
                CustomTile groundTile = groundTilemap.GetTile<CustomTile>(cell);
                if (groundTile?.biomeData != null)
                {
                    if (!biomeGroups.ContainsKey(groundTile.biomeData))
                    {
                        biomeGroups[groundTile.biomeData] = new();
                        biomeTileCache[groundTile.biomeData] = groundTile;
                    }
                    biomeGroups[groundTile.biomeData].Add(cell);
                }

                // Görev grubu
                CustomTile missionTile = missionTilemap.GetTile<CustomTile>(cell);
                if (missionTile?.missionData != null)
                {
                    if (!missionGroups.ContainsKey(missionTile.missionData))
                    {
                        missionGroups[missionTile.missionData] = new();
                        missionTileCache[missionTile.missionData] = missionTile;
                    }
                    missionGroups[missionTile.missionData].Add(cell);
                }
            }
    }

    // ——— TEXTURE ÜRETME ———

    void BuildBiomeTexture()
    {
        Color[] colorPixels = new Color[texWidth * texHeight];
        Color[] indexPixels = new Color[texWidth * texHeight];

        foreach (var (biome, cells) in biomeGroups)
        {
            if (!biomeTileCache.TryGetValue(biome, out var tile)) continue;

            foreach (var cell in cells)
            {
                int idx = CellToIndex(cell);
                if (idx < 0) continue;

                // Renk texture
                colorPixels[idx] = new Color(
                    tile.overlayColor.r,
                    tile.overlayColor.g,
                    tile.overlayColor.b,
                    tile.overlayIntensity    // A: görünürlük
                );

                // Index texture
                indexPixels[idx] = new Color(
                    (float)tile.textureIndex / maxBiomes,
                    0, 0, 1f
                );
            }
        }

        BiomeTexture = CreateTexture(colorPixels);
        BiomeIndexTexture = CreateIndexTexture(indexPixels);
    }

    void BuildMissionTexture()
    {
        Color[] pixels = new Color[texWidth * texHeight];

        foreach (var (mission, cells) in missionGroups)
        {
            if (!missionTileCache.TryGetValue(mission, out var tile)) continue;

            float missionType = mission.missionType switch
            {
                MissionType.ExtinguishZone => 0.5f,
                MissionType.ContainmentZone => 1.0f,
                _ => 0f
            };

            foreach (var cell in cells)
            {
                int idx = CellToIndex(cell);
                if (idx < 0) continue;

                pixels[idx] = new Color(
                    missionType,        // R: görev tipi
                    tile.overlayBlend,  // G: şeffaflık
                    0f,                 // B: aktif
                    1f                  // A: tile var
                );
            }
        }

        MissionTexture = CreateTexture(pixels);
    }

    // ——— GÖREV DURUMU GÜNCELLEME ———

    public void UpdateMissionState(MissionData mission, MissionVisualState state)
    {
        if (!missionGroups.TryGetValue(mission, out var cells)) return;

        Color[] pixels = MissionTexture.GetPixels();

        float stateValue = state switch
        {
            MissionVisualState.Active => 0.0f,
            MissionVisualState.Completed => 0.5f,
            MissionVisualState.Failed => 1.0f,
            _ => 0f
        };

        foreach (var cell in cells)
        {
            int idx = CellToIndex(cell);
            if (idx < 0) continue;
            pixels[idx].b = stateValue;
        }

        MissionTexture.SetPixels(pixels);
        MissionTexture.Apply(false);
    }

    // ——— YARDIMCI ———

    Texture2D CreateTexture(Color[] pixels)
    {
        var tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(pixels);
        tex.Apply(false);
        return tex;
    }

    Texture2D CreateIndexTexture(Color[] pixels)
    {
        var tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Point,  // ← Bilinear değil Point!
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(pixels);
        tex.Apply(false);
        return tex;
    }

    int CellToIndex(Vector3Int cell)
    {
        int x = cell.x - bounds.xMin;
        int y = cell.y - bounds.yMin;
        if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return -1;
        return y * texWidth + x;
    }

    void OnDestroy()
    {
        if (BiomeTexture != null) Destroy(BiomeTexture);
        if (MissionTexture != null) Destroy(MissionTexture);
    }
}

public enum MissionVisualState { Active, Completed, Failed }