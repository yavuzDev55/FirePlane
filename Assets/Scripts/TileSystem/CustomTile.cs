using UnityEngine;
using UnityEngine.Tilemaps;

// This attribute allows us to create CustomTile assets directly in the Unity Editor 
// via the 'Create' menu. This is essential because the Tilemap system requires 
// each tile to be a distinct asset (ScriptableObject).
[CreateAssetMenu(fileName = "New Custom Tile", menuName = "Custom/Custom Tile")]
public class CustomTile : Tile
{
    // A reference to the ScriptableObject that holds the biome properties (fuel, resistance).
    public BiomeData biomeData;

    // A reference to the ScriptableObject that holds the mission properties (mission type).
    public MissionData missionData;

    // The sprite used for rendering this tile on the map and in the palette.
    [Tooltip("The visual representation of this tile.")]
    public Sprite tileSprite; 
    
    // Called when the Tile is placed on the Tilemap or when the tile's data is requested.
    // We use this to ensure the correct sprite is displayed.
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        
        // Assign the public sprite variable to the actual TileData struct used for rendering.
        tileData.sprite = tileSprite; 
    }
}
