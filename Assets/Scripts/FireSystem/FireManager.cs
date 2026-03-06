using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class FireManager : MonoBehaviour
{
    [Header("Tilemap References")]
    public Tilemap groundTilemap;        // Holds biome data tiles
    public Tilemap fireStateTilemap;      // Visual fire state (burning / ash)

    [Header("Fire State Tiles")]
    public TileBase igniteMarkerTile;     // Initial fire markers
    public TileBase burningTile;
    public TileBase ashTile;

    [Header("Simulation Settings")]
    public float spreadCheckInterval = 0.35f; // How often heat is applied
    private float spreadTimer;

    [Tooltip("Base burn duration multiplied by biome fuelValue")]
    public float baseCellMaxBurnTime = 2f;

    [Header("Heat Settings")]
    public float baseHeatPerNeighbor = 20f;
    public float diagonalHeatMultiplier = 0.6f;

    [Header("Extinguishing Settings")]
    public float baseExtinguishResistance = 100f;

    [Header("UI")]
    public UIDocument gameScreenUI;
    Label firingCellsLabel;
    Label ashCellsLabel;
    int ashCellCountCache;

    // --- Runtime State ---

    // Active burning cells (used as heat sources)
    private HashSet<Vector3Int> activeBurningCells = new();

    // Burn timer per burning cell
    private Dictionary<Vector3Int, float> cellBurnTimers = new();

    // Ignition resistance derived from biome fireSpreadResistance
    private Dictionary<Vector3Int, float> cellIgnitionResistance = new();

    // Extinguish resistance for manual firefighting
    private Dictionary<Vector3Int, float> cellExtinguishHealth = new();

    // Cells that will ignite AFTER spread calculation (avoids collection modification)
    private List<Vector3Int> cellsToIgnite = new();

    // Flammable objects grouped by tile cell
    private Dictionary<Vector3Int, List<FlammableObject>> flammablesByCell = new();


    // --- Unity Lifecycle ---

    void Start()
    {
        InitializeFire();
    }

    void Update()
    {
        spreadTimer += Time.deltaTime;

        // Apply heat at fixed intervals
        if (spreadTimer >= spreadCheckInterval)
        {
            ProcessFireSpread();
            spreadTimer = 0f;
        }

        // Burning progression happens every frame
        ProcessCellBurnout();
    
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

    // --- Helper Methods ---

    public bool IsCellBurning(Vector3Int cellPos)
    {
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

        return true;
    }

    
    // --- Initialization ---
    // Converts ignite marker tiles into active burning cells at game start
    void InitializeFire()
    {
        if (fireStateTilemap == null || igniteMarkerTile == null) return;

        BoundsInt bounds = fireStateTilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);

                if (fireStateTilemap.GetTile(cell) == igniteMarkerTile)
                {
                    SetCellOnFire(cell);
                    fireStateTilemap.SetTile(cell, null);
                }
            }
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
            float heat = baseHeatPerNeighbor;
            if (IsDiagonal(offset))
                heat *= diagonalHeatMultiplier;

            // Micro randomness affects only timing, not outcome
            heat *= Random.Range(0.9f, 1.1f);

            // Reduce ignition resistance over time
            cellIgnitionResistance[neighbor] -= heat * spreadCheckInterval;

            // If resistance is depleted, mark for ignition
            if (cellIgnitionResistance[neighbor] <= 0f && CanCellCatchFire(neighbor))
            {
                cellIgnitionResistance.Remove(neighbor);

                if (!cellsToIgnite.Contains(neighbor))
                    cellsToIgnite.Add(neighbor);
            }
        }
    }

    // --- Burning & Burnout ---

    // Handles burning duration and conversion to ash
    void ProcessCellBurnout()
    {
        List<Vector3Int> cellsToBurnout = new();
        var keys = new List<Vector3Int>(cellBurnTimers.Keys);

        foreach (var cell in keys)
        {
            CustomTile groundTile = groundTilemap.GetTile<CustomTile>(cell);
            float fuelMultiplier = groundTile != null
                ? groundTile.biomeData.fuelValue
                : 1f;

            float maxBurnTime = baseCellMaxBurnTime * fuelMultiplier;
            cellBurnTimers[cell] += Time.deltaTime;

            if (cellBurnTimers[cell] >= maxBurnTime)
                cellsToBurnout.Add(cell);
        }

        foreach (var cell in cellsToBurnout)
        {
            ExtinguishCell(cell, true);
        }
    }

    // --- Interaction ---

    // Starts burning state for a cell
    public void SetCellOnFire(Vector3Int cellPos)
    {
        if (IsCellBurning(cellPos)) return;

        fireStateTilemap.SetTile(cellPos, burningTile);
        cellBurnTimers[cellPos] = 0f;
        activeBurningCells.Add(cellPos);

        IgniteFlammablesInCell(cellPos);
    }

    // Applies extinguisher damage using world position
    public void ApplyExtinguisherDamageToCell(Vector3 worldPos, float extinguishDamage)
    {
        Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);

        if (!IsCellBurning(cellPos)) return;

        if (!cellExtinguishHealth.ContainsKey(cellPos))
            cellExtinguishHealth[cellPos] = baseExtinguishResistance;

        cellExtinguishHealth[cellPos] -= extinguishDamage;

        if (cellExtinguishHealth[cellPos] <= 0f)
        {
            cellExtinguishHealth.Remove(cellPos);
            ExtinguishCell(cellPos, false);
        }
    }

    // Removes fire from a cell (burnout or extinguish)
    public void ExtinguishCell(Vector3Int cellPos, bool burntOut)
    {
        fireStateTilemap.SetTile(cellPos, burntOut ? ashTile : null);

        activeBurningCells.Remove(cellPos);
        cellBurnTimers.Remove(cellPos);
        cellIgnitionResistance.Remove(cellPos);
        cellExtinguishHealth.Remove(cellPos);

        ExtinguishFlammablesInCell(cellPos, burntOut);

        if (burntOut) ashCellCountCache++;
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
        if (!flammablesByCell.TryGetValue(cellPos, out var list))
            return;

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

}
