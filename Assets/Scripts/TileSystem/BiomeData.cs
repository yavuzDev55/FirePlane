using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New BiomeData", menuName = "Custom/BiomeData")]
public class BiomeData : ScriptableObject
{
    [Header("Biome properties")]
    public string biomeName;

    [Tooltip("Fire speed/spread. High value is burning more time.")]
    public float fuelValue = 1.0f; 

    [Tooltip("It reduces speed of spreading of this tile. 0 = no impact, 1 = full resistance.")]
    [Range(0f, 1f)]public float fireSpreadResistance = 0.0f;

    [Tooltip("Is this biome considered as water?")]
    public bool isWater = false;

    [Header("Vegetation Settings")]
    public bool canSpawnVegetation = true;

    public List<VegetationEntry> vegetationList;
}

[System.Serializable]
public class VegetationEntry
{
    public GameObject prefab;
    [Range(0f, 1f)]
    public float spawnWeight;
}
