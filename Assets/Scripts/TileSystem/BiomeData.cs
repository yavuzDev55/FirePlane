using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New BiomeData", menuName = "Custom/BiomeData")]
public class BiomeData : ScriptableObject
{
    [Header("Biome properties")]
    public string biomeName;
    
    [Tooltip("Is this biome considered as water?")]
    public bool isWater = false;

    [Tooltip("Fire speed/spread. High value is burning more time.")]
    public float fuelValue = 1.0f; 

    [Tooltip("It reduces speed of spreading of this tile. 0 = no impact, 1 = full resistance.")]
    [Range(0f, 1f)]public float fireSpreadResistance = 0.0f;

    [Range(0f, 1f)]
    [Tooltip("Maximum heat level this biome can reach (0-1 normalized)")]
    public float maxHeat = 0.7f;
    
    [Range(0f, 0.5f)]
    [Tooltip("Fuel consumption rate per update cycle")]
    public float burnRate = 0.15f;
    
    [Range(0.6f, 1.5f)]
    [Tooltip("Resistance to extinguishing (1 = normal, >1 = harder to extinguish)")]
    public float extinguishResistance = 0.8f;
    
    [Range(0f, 0.2f)]
    [Tooltip("Drying rate when extinguished state (higher = faster drying)")]
    public float dryingRate = 0.05f;

    [Header("Fire System - Raging Conditions")]
    
    [Tooltip("Can this biome rage when wind is strong (windStrength > 0.5)?")]
    public bool canRageInWind = true;
    
    [Tooltip("Can this biome rage when fuel is abundant?")]
    public bool canRageWithFuel = false;
    
    [Range(0f, 2f)]
    [Tooltip("Minimum fuel level required to trigger fuel-based raging")]
    public float minFuelForRaging = 1.0f;
    
    [Range(0.5f, 5f)]
    [Tooltip("Maximum duration of raging state (in seconds)")]
    public float ragingMaxDuration = 2f;

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
