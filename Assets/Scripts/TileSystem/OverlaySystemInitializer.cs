using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlaySystemInitializer : MonoBehaviour
{
    [Header("References")]
    public TilemapTextureBuilder textureBuilder;
    public Tilemap groundTilemap;

    public BiomeOverlayRenderer   biomeOverlay;
    public MissionOverlayRenderer missionOverlay;

    void Start()
    {
        biomeOverlay.Init(
            textureBuilder.BiomeTexture,
            textureBuilder.BiomeIndexTexture,  // YENİ
            groundTilemap
        );

        missionOverlay.Init(
            textureBuilder.MissionTexture,
            groundTilemap
        );
    }
}