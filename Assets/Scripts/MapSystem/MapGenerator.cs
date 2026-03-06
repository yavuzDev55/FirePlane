using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Tilemap References")]
    public Tilemap groundTilemap;

    [Header("Parents")]
    public Transform environmentParent;
    public GameObject parentDynamicObjects;

    void Start()
    {
        if (environmentParent == null)
            environmentParent = new GameObject("Environment Objects").transform;

        GenerateVegetation();
    }

    void GenerateVegetation()
    {
        BoundsInt bounds = groundTilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                CustomTile tile = groundTilemap.GetTile<CustomTile>(cellPos);

                if (tile == null)
                    continue;

                BiomeData biome = tile.biomeData;

                if (!biome.canSpawnVegetation)
                    continue;

                if (biome.vegetationList == null || biome.vegetationList.Count == 0)
                    continue;

                GameObject prefabToSpawn = ChooseVegetation(biome.vegetationList);
                if (prefabToSpawn == null)
                    continue;

                SpawnVegetation(prefabToSpawn, cellPos);
            }
        }
    }

    GameObject ChooseVegetation(List<VegetationEntry> list)
    {
        float totalWeight = 0f;

        foreach (var v in list)
            totalWeight += v.spawnWeight;

        float randomValue = Random.value * totalWeight;
        float current = 0f;

        foreach (var v in list)
        {
            current += v.spawnWeight;
            if (randomValue <= current)
                return v.prefab;
        }

        return null;
    }

    void SpawnVegetation(GameObject prefab, Vector3Int cellPos)
    {
        Vector3 worldPos = groundTilemap.CellToWorld(cellPos);

        float offsetX = Random.Range(-0.3f, 0.3f);
        float offsetY = Random.Range(-0.15f, 0.15f);
        Vector3 offset = new Vector3(offsetX, offsetY + 0.25f, 0f);

        GameObject plant = Instantiate(
            prefab,
            worldPos + offset,
            Quaternion.identity,
            environmentParent
        );

        if (parentDynamicObjects != null)
            plant.transform.parent = parentDynamicObjects.transform;

        SpriteRenderer sr = plant.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sortingOrder = -Mathf.RoundToInt(worldPos.y * 100);
    }
}
