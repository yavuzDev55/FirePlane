using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Custom Tile", menuName = "Custom/Custom Tile")]
public class CustomTile : Tile
{
    public BiomeData biomeData;
    public MissionData missionData;
    public Sprite tileSprite;

    [Header("Shader Visual Properties")]
    public Color overlayColor = Color.white;
    [Range(0f, 1f)] public float overlayIntensity = 1f;
    [Range(0f, 1f)] public float overlayBlend = 0.5f;

    [Header("Texture Array")]
    [Tooltip("BiomeTextureArray içindeki sıra (0=grassland, 1=forest...)")]
    public int textureIndex = 0;

    public override void GetTileData(
        Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        tileData.sprite = tileSprite;
    }
}