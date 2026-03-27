using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class FireManager : MonoBehaviour
{
    [Header("Tilemap References")]
    public Tilemap groundTilemap;        // Holds biome data tiles
    public Tilemap fireStateTilemap;      // Visual fire state (burning / ash)

    [Header("Fire State Tiles")]
    public TileBase igniteMarkerTile;     // Initial fire markers
    public TileBase ignitingTile;         // New: tutuşmaya başladı
    public TileBase burningTile;
    public TileBase ragingTile;           // New: parlak alev (tehlikeli)
    public TileBase ashTile;
    public TileBase wetTile;     // New: söndürülmüş (kurutma süreci)

    [Header("Simulation Settings")]
    public float spreadCheckInterval = 0.5f;  // How often heat is applied
    public float updateInterval = 0.1f;         // How often cells update state
    private float spreadTimer;
    private float updateTimer;

    [Tooltip("Base burn duration multiplied by biome fuelValue")]
    public float baseCellMaxBurnTime = 2f;

    [Header("Heat Settings")]
    public float baseHeatPerNeighbor = 20f;
    public float diagonalHeatMultiplier = 0.6f;

    [Header("Extinguishing Settings")]
    public float baseExtinguishResistance = 100f;

    [Header("Global Settings")]
    public float windStrength = 0f;             // -1 to 1 (affects spread and raging)
    public float ambientMoisture = 0.3f;        // 0-1 (affects ignition)

    [Header("UI")]
    public UIDocument gameScreenUI;
    Label firingCellsLabel;
    Label ashCellsLabel;
    int ashCellCountCache;

    // --- Runtime State ---

    // Active burning cells (used as heat sources)
    // Fire cell data (state, heat, fuel, wetness, extinguish points)
    private Dictionary<Vector3Int, FireCellData> cellData = new();

    // Active burning cells (used as heat sources)
    private HashSet<Vector3Int> activeBurningCells = new();

    // Burn timer per burning cell (legacy, kept for compatibility)
    private Dictionary<Vector3Int, float> cellBurnTimers = new();
    // Ignition resistance derived from biome fireSpreadResistance
    private Dictionary<Vector3Int, float> cellIgnitionResistance = new();

    // Extinguish resistance for manual firefighting (legacy)
    private Dictionary<Vector3Int, float> cellExtinguishHealth = new();

    // Cells that will ignite AFTER spread calculation (avoids collection modification)
    private List<Vector3Int> cellsToIgnite = new();

    // Flammable objects grouped by tile cell
    private Dictionary<Vector3Int, List<FlammableObject>> flammablesByCell = new();

    // BiomeData cache for performance
    private Dictionary<Vector3Int, BiomeData> biomeDataCache = new();

    // --- Unity Lifecycle ---

    private void Awake()
    {
        CacheBiomeData();
    }

    void Start()
    {
        Invoke("InitializeFire", 0.1f); // Delay to ensure all systems are ready
    }

    void Update()
    {
        spreadTimer += Time.deltaTime;
        updateTimer += Time.deltaTime;

        // Cell state updates (heat, fuel, wetness changes)
        if (updateTimer >= updateInterval)
        {
            ProcessCellUpdates();
            updateTimer = 0f;
        }

        // Apply heat at fixed intervals
        if (spreadTimer >= spreadCheckInterval)
        {
            ProcessFireSpread();
            spreadTimer = 0f;
        }
    
        // UI Update
        if (gameScreenUI != null)
        {
            if (firingCellsLabel == null)
            {
                firingCellsLabel = gameScreenUI.rootVisualElement.Q<Label>("FiringCellsLabel");
                ashCellsLabel = gameScreenUI.rootVisualElement.Q<Label>("AshCellsLabel");
            }

            if (firingCellsLabel != null && ashCellsLabel != null)
            {
                firingCellsLabel.text = "Burning Cells: " + activeBurningCells.Count;
                ashCellsLabel.text = "Ash Cells: " + ashCellCountCache;
            }
        }
    }

    // --- Cache Initialization ---

    void CacheBiomeData()
    {
        // Cache all biome data for performance
        BoundsInt bounds = groundTilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                CustomTile tile = groundTilemap.GetTile<CustomTile>(cell);
                
                if (tile != null && tile.biomeData != null)
                {
                    biomeDataCache[cell] = tile.biomeData;
                }
            }
        }
    }


    // --- Helper Methods ---
    public bool IsCellBurning(Vector3Int cellPos)
    {
        if (cellData.TryGetValue(cellPos, out var data))
        {
            return data.state == FireState.IGNITING || 
                   data.state == FireState.BURNING || 
                   data.state == FireState.RAGING;
        }
        return fireStateTilemap.GetTile(cellPos) == burningTile;
    }

    private Vector3Int[] GetNeighborOffsets()
    {
        return new Vector3Int[]
        {
            new( 1, 0, 0), new(-1, 0, 0),
            new( 0, 1, 0), new( 0,-1, 0),
            new( 1, 1, 0), new(-1,-1, 0),
            new( 1,-1, 0), new(-1, 1, 0)
        };
    }

    private bool IsDiagonal(Vector3Int offset)
    {
        return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 2;
    }


    private bool CanCellCatchFire(Vector3Int cellPos)
    {
        CustomTile tile = groundTilemap.GetTile<CustomTile>(cellPos);
        if (tile == null || tile.biomeData == null)
            return false;

        // Fire resistance of 1 or higher means this tile NEVER burns
        if (tile.biomeData.fireSpreadResistance >= 1f)
            return false;

        // Water tiles cannot burn
        if (tile.biomeData.isWater)
            return false;

        return true;
    }

    BiomeData GetBiomeData(Vector3Int cellPos)
    {
        if (biomeDataCache.TryGetValue(cellPos, out var biome))
            return biome;

        CustomTile tile = groundTilemap.GetTile<CustomTile>(cellPos);
        if (tile != null && tile.biomeData != null)
        {
            biomeDataCache[cellPos] = tile.biomeData;
            return tile.biomeData;
        }

        return null;
    }

    FireCellData GetOrCreateCellData(Vector3Int cellPos)
    {
        if (!cellData.TryGetValue(cellPos, out var data))
        {
            BiomeData biome = GetBiomeData(cellPos);
            data = new FireCellData
            {
                state = FireState.NORMAL,
                heat = 0f,
                fuel = biome != null ? biome.fuelValue : 1f,
                wetness = 0f,
                extinguishPoints = 0f,
                rageDuration = 0f,
                timeBurning = 0f
            };

            cellData[cellPos] = data;
        }

        return data;
    }

    // --- Initialization ---
    // Converts ignite marker tiles into active burning cells at game start

    void InitializeFire()
    {
        Debug.Log("Initializing fire from ignite markers...");
        if (fireStateTilemap == null || igniteMarkerTile == null) return;

        BoundsInt bounds = fireStateTilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);

                if (fireStateTilemap.GetTile(cell) == igniteMarkerTile)
                {
                    fireStateTilemap.SetTile(cell, null);
                    SetCellOnFire(cell);
                }
            }
        }
    }

    // --- Fire State Updates (Per-Cell) ---

    void ProcessCellUpdates()
    {
        // Update all cells in burning states
        var cellsToProcess = new List<Vector3Int>(cellData.Keys);

        foreach (var cellPos in cellsToProcess)
        {
            if (!cellData.TryGetValue(cellPos, out var data))
                continue;

            UpdateCellState(cellPos, data);
        }
    }

    void UpdateCellState(Vector3Int cellPos, FireCellData data)
    {
        BiomeData biome = GetBiomeData(cellPos);
        if (biome == null)
            return;

        // === NORMAL STATE ===
        if (data.state == FireState.NORMAL)
        {
            // Drying from extinguished
            if (data.wetness > 0)
            {
                data.state = FireState.WET;
                UpdateVisual(cellPos);
            }
            return;
        }

        // === IGNITING STATE ===
        if (data.state == FireState.IGNITING)
        {
            // Heat gain during ignition
            float heatGain = 0.3f;
            data.heat = Mathf.Min(biome.maxHeat, data.heat + heatGain * updateInterval);

            // Moisture inhibits ignition
            if (ambientMoisture > 0.6f)
            {
                data.heat *= 0.5f;
            }

            // Transition to burning when heat is sufficient
            if (data.heat >= biome.maxHeat * 0.5f)
            {
                data.state = FireState.BURNING;
                UpdateVisual(cellPos);
            }

            // Söndürme kontrolü
            if (data.extinguishPoints >= biome.extinguishResistance)
            {
                TransitionToExtinguished(cellPos, false);
                return;
            }
            return;
        }

        // === BURNING STATE ===
        if (data.state == FireState.BURNING)
        {
            float burnAmount = biome.burnRate * updateInterval;
            data.fuel = Mathf.Max(0, data.fuel - burnAmount);
            data.timeBurning += updateInterval;

            // HEAT KONTROL EKLE (fuel varsa):
            if (data.fuel > 0)
            {
                // Fuel varsa heat sabit tut
                data.heat = Mathf.Min(biome.maxHeat, data.heat + (burnAmount * 0.5f));
            }
            else
            {
                TransitionToExtinguished(cellPos, true);
                return;
            }

            // Söndürme kontrolü
            if (data.extinguishPoints >= biome.extinguishResistance)
            {
                TransitionToExtinguished(cellPos, false);
                return;
            }

            // Check raging conditions
            CheckRagingConditions(cellPos, data, biome);

            return;
        }

        // === RAGING STATE ===
        if (data.state == FireState.RAGING)
        {
            // 2x fuel consumption during raging
            float burnAmount = biome.burnRate * 2f * updateInterval;
            data.fuel = Mathf.Max(0, data.fuel - burnAmount);
            data.heat = biome.maxHeat;
            data.rageDuration += updateInterval;

            // Maximum raging duration
            if (data.rageDuration >= biome.ragingMaxDuration)
            {
                data.state = FireState.BURNING;
                data.rageDuration = 0f;
                UpdateVisual(cellPos);
            }

            // Extinguish resistance increased during raging (1.5x)
            float ragingMultiplier = 1.5f;
            if (data.extinguishPoints >= biome.extinguishResistance * ragingMultiplier)
            {
                TransitionToExtinguished(cellPos, false);
                return;
            }

            // Fuel depletion ends raging
            if (data.fuel <= 0)
            {
                data.state = FireState.BURNING;
                data.rageDuration = 0f;
                UpdateVisual(cellPos);
            }

            return;
        }

        // === ASH STATE ===
        if (data.state == FireState.ASH)
        {
            data.heat = Mathf.Max(0, data.heat - 0.3f * updateInterval);

            return;
        }

        // === EXTINGUISHED STATE ===
        if (data.state == FireState.WET)
        {
            // Drying process
            data.wetness = Mathf.Max(0, data.wetness - biome.dryingRate * updateInterval);

            // Return to normal when dry
            if (data.wetness <= 0)
            {
                data.state = FireState.NORMAL;
                data.extinguishPoints = 0f;
                UpdateVisual(cellPos);
            }
            return;
        }
    }

    void CheckRagingConditions(Vector3Int cellPos, FireCellData data, BiomeData biome)
    {
        // Basic condition: sufficient heat
        bool heatEnough = data.heat >= biome.maxHeat * 0.95f;

        if (!heatEnough)
            return;

        // Condition 1: Wind + biom support
        if (biome.canRageInWind && windStrength > 0.5f)
        {
            data.state = FireState.RAGING;
            data.rageDuration = 0f;
            UpdateVisual(cellPos);
            return;
        }

        // Condition 2: Fuel + biom support
        if (biome.canRageWithFuel && data.fuel >= biome.minFuelForRaging)
        {
            data.state = FireState.RAGING;
            data.rageDuration = 0f;
            UpdateVisual(cellPos);
            return;
        }
    }

    // --- Fire Spread Logic (Heat Accumulation Model) ---

    void ProcessFireSpread()
    {
        cellsToIgnite.Clear();

        // Iterate ONLY over current burning cells
        foreach (var burningCell in activeBurningCells)
        {
            ApplyHeatToNeighbors(burningCell);
        }

        // Ignite collected cells AFTER iteration (safe)
        foreach (var cell in cellsToIgnite)
        {
            SetCellOnFire(cell);
        }
    }

    // Applies heat from a burning cell to its neighbors
    private void ApplyHeatToNeighbors(Vector3Int sourceCell)
    {
        if (!cellData.TryGetValue(sourceCell, out var sourceData))
            return;

        foreach (var offset in GetNeighborOffsets())
        {
            Vector3Int neighbor = sourceCell + offset;

            // Skip already burned or burning cells
            if (IsCellBurning(neighbor) ||
                fireStateTilemap.GetTile(neighbor) == ashTile)
                continue;

            CustomTile groundTile = groundTilemap.GetTile<CustomTile>(neighbor);
            if (groundTile == null) continue;

            // Initialize ignition resistance once per cell
            if (!cellIgnitionResistance.ContainsKey(neighbor))
            {
                float baseResistance = groundTile.biomeData.fireSpreadResistance * 100f;
                float randomFactor = Random.Range(0.85f, 1.15f);
                cellIgnitionResistance[neighbor] = baseResistance * randomFactor;
            }

            // Calculate heat contribution
            float heat = baseHeatPerNeighbor * (sourceData.heat / groundTile.biomeData.maxHeat);
            if (IsDiagonal(offset))
                heat *= diagonalHeatMultiplier;

            // Micro randomness
            heat *= Random.Range(0.9f, 1.1f);

            // Wind effect
            if (windStrength > 0)
            {
                int windX = windStrength > 0 ? 1 : -1;
                float alignment = offset.x * windX;
                if (alignment > 0)
                    heat *= (1f + windStrength);
            }

            var neighborData = GetOrCreateCellData(neighbor);

            if (neighborData.wetness > 0)
            {
                neighborData.wetness = Mathf.Max(0, neighborData.wetness - heat * spreadCheckInterval);
            }
            else
            {
                // Reduce ignition resistance
                cellIgnitionResistance[neighbor] -= heat * spreadCheckInterval;
            }

            // If resistance is depleted, mark for ignition
            if (cellIgnitionResistance[neighbor] <= 0f && CanCellCatchFire(neighbor))
            {
                cellIgnitionResistance.Remove(neighbor);

                if (!cellsToIgnite.Contains(neighbor))
                    cellsToIgnite.Add(neighbor);
            }
        }
    }

    // --- Interaction ---

    // Starts burning state for a cell
    public void SetCellOnFire(Vector3Int cellPos)
    {
        if (IsCellBurning(cellPos)) 
            return;

        if (!CanCellCatchFire(cellPos))
            return;

        var data = GetOrCreateCellData(cellPos);
        data.state = FireState.IGNITING;
        data.heat = 0.1f;

        UpdateVisual(cellPos);
        activeBurningCells.Add(cellPos);

        IgniteFlammablesInCell(cellPos);
    }

    // External script applies extinguish (accumulates points)
    public void ApplyExtinguish(Vector3Int cellPos, float extinguishAmount)
    {
        var data = GetOrCreateCellData(cellPos);
        BiomeData biome = GetBiomeData(cellPos);
        if (data.state == FireState.IGNITING || data.state == FireState.BURNING || data.state == FireState.RAGING)
        {
            float neededToExtinguish = Mathf.Max(0,
                biome.extinguishResistance - data.extinguishPoints);
            float extinguishUsed = Mathf.Min(extinguishAmount, neededToExtinguish);

            float remainingForWetness = extinguishAmount - extinguishUsed;

            data.extinguishPoints += extinguishUsed;
            data.heat = Mathf.Max(0, data.heat - extinguishUsed * 0.1f);

            if (remainingForWetness > 0)
            {
                data.wetness = Mathf.Min(1f, data.wetness + remainingForWetness);
            }
        }
        else if(data.state == FireState.NORMAL || data.state == FireState.WET)
        {
            if (!CanCellCatchFire(cellPos)) return;
            data.wetness = Mathf.Min(1f, data.wetness + extinguishAmount);
        }
    }

    void TransitionToExtinguished(Vector3Int cellPos, bool burnout)
    {
        if (!cellData.TryGetValue(cellPos, out var data))
            return;

        if (burnout)
        { 
            data.state = FireState.ASH;
        }
        else
        {
            data.state = FireState.WET;
            data.extinguishPoints = 0f;
            data.heat = 0f;
        }

        UpdateVisual(cellPos);
        activeBurningCells.Remove(cellPos);
        cellIgnitionResistance.Remove(cellPos);

        ExtinguishFlammablesInCell(cellPos, burnout);

        if(burnout) ashCellCountCache++;
    }

    void UpdateVisual(Vector3Int cellPos)
    {
        if (!cellData.TryGetValue(cellPos, out var data))
            return;

        TileBase tile = data.state switch
        {
            FireState.NORMAL => null,
            FireState.IGNITING => ignitingTile,
            FireState.BURNING => burningTile,
            FireState.RAGING => ragingTile,
            FireState.ASH => ashTile,
            FireState.WET => wetTile,
            _ => null
        };

        fireStateTilemap.SetTile(cellPos, tile);
    }

    // --- Object Interaction ---
    
    public void RegisterFlammable(Vector3Int cellPos, FlammableObject obj)
    {
        if (!flammablesByCell.TryGetValue(cellPos, out var list))
        {
            list = new List<FlammableObject>();
            flammablesByCell[cellPos] = list;
        }

        if (!list.Contains(obj))
            list.Add(obj);
    }

    public void UnregisterFlammable(Vector3Int cellPos, FlammableObject obj)
    {
        if (flammablesByCell.TryGetValue(cellPos, out var list))
        {
            list.Remove(obj);
        }
    }

    // Ignites flammable objects located on a burning tile
    private void IgniteFlammablesInCell(Vector3Int cellPos)
    {
        if (!flammablesByCell.TryGetValue(cellPos, out var list)) {
            return;
        }

        foreach (var obj in list)
        {
            if (obj != null && !obj.IsBurning)
                obj.Ignite();
        }
    }

    private void ExtinguishFlammablesInCell(Vector3Int cellPos, bool burntOut)
    {
        if (!flammablesByCell.TryGetValue(cellPos, out var list))
            return;

        foreach (var obj in list)
        {
            if (obj != null && obj.IsBurning)
                obj.Extinguish();
            if (burntOut) Destroy(obj.gameObject);
        }
    }

    // --- Public API ---

    public void SetWind(float strength)
    {
        windStrength = Mathf.Clamp(strength, -1f, 1f);
    }

    public void SetAmbientMoisture(float moisture)
    {
        ambientMoisture = Mathf.Clamp01(moisture);
    }

    public FireCellData GetCellData(Vector3Int cellPos)
    {
        return cellData.TryGetValue(cellPos, out var data) ? data : null;
    }

}

// --- Fire State ---
public enum FireState
{
    NORMAL = 0,
    IGNITING = 1,
    BURNING = 2,
    RAGING = 3,
    ASH = 4,
    WET = 5
}

// --- Fire Cell Data ---
public class FireCellData
{
    public FireState state = FireState.NORMAL;
    public float heat = 0f;              // 0-1 normalized
    public float fuel = 1f;              // 0-1 normalized
    public float wetness = 0f;           // For drying process
    public float extinguishPoints = 0f;  // Extinguish accumulation
    public float rageDuration = 0f;      // Raging duration tracking
    public float timeBurning = 0f;       // Total burning time (statistics)
}
