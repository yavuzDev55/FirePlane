using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class VehicleWaterTank : MonoBehaviour
{
    [Header("References")]
    public FireManager fireManager;
    public Tilemap groundTilemap;
    public Transform waterDropPoint;
    public BoxCollider2D intakeCollider;
    public VehicleController vehicleController;

    [Header("Extinguishing")]
    public float halfLength = 2.5f;    // world units
    public float maxDamage = 100f;
    public float minDamage = 60f;
    public float extinguishingPerSecond = 2f;
    public float minExtinguishDelay = 0.05f;
    public float maxExtraDelay = 0.25f;
    public bool isExtinguishing;

    [Header("Intake")]
    public float fillRate = 20f;
    public bool isIntaking;

    [Header("Water Tank")]
    public float maxWater = 100f;
    public float currentWater = 100f;
    public float waterConsumptionRate = 10f;

    [Header("Input")]
    public InputActionReference dropWaterAction;
    //public InputActionReference intakeAction;

    [Header("UI")]
    public UIDocument gameScreenUI;
    VisualElement backgroundBar;
    VisualElement fillBar;
    Label waterTankLabel;
    public Color[] backgroundColors;

    private struct PendingExtinguish
    {
        public Vector3 worldPos;
        public float timeToExecute;
        public float damage;
    }

    private List<PendingExtinguish> _pendingExtinguishes = new();


    private List<Vector2> pointstoExtinguish = new();

    Vector2 lastDropPos;
    bool hasLastPos;
    float lastDropTime;
    bool wasLastExtinguish;

    // Isometric Z As Y projeksiyon sabitleri
    float yScale; // Y sıkıştırmasını geri açar (cellSize.x / cellSize.y = 2f)
    float zToY;   // Z'nin Y'ye katkısı  (cellSize.y / cellSize.x = 0.5f)

    struct DebugWaterHit
    {
        public Vector3 worldPos;
        public float waterAmount;
    }

    List<DebugWaterHit> debugHits = new();

    void Awake()
    {
        if (fireManager == null)
            fireManager = FindAnyObjectByType<FireManager>();
    }

    void Start()
    {
        if (groundTilemap == null)
            groundTilemap = fireManager.groundTilemap;

        Grid grid = groundTilemap.layoutGrid;
        Vector3 cs = grid.cellSize;
        yScale = cs.x / cs.y;  // 2f  — iso Y sıkıştırmasını açar
        zToY = cs.y / cs.x;  // 0.5f — Z'nin ekran Y'sine katkısı
    }

    void OnEnable()
    {
        dropWaterAction.action.Enable();
        //intakeAction.action.Enable();

        dropWaterAction.action.started += _ => StartExtinguishing();
        dropWaterAction.action.canceled += _ => StopExtinguishing();
    }

    void OnDisable()
    {
        dropWaterAction.action.started -= _ => StartExtinguishing();
        dropWaterAction.action.canceled -= _ => StopExtinguishing();

        dropWaterAction.action.Disable();
        //intakeAction.action.Disable();
    }

    void Update()
    {
        //Water Intake and Extinguishing Logic
        if (isExtinguishing)
        {
            //Water consumption
            if (currentWater <= 0f)
            {
                StopExtinguishing();
                return;
            }

            ExtinguishAlongMovement();

            currentWater -= waterConsumptionRate * Time.deltaTime;// /*-*/
            if (currentWater < 0f)
                currentWater = 0f;
        }

        else if (isIntaking)
        {
            FillWater();
        }

        //UI Update
        if (gameScreenUI != null)
        {
            if (backgroundBar == null || fillBar == null || waterTankLabel == null)
            {
                backgroundBar = gameScreenUI.rootVisualElement.Q<VisualElement>("BarBackground");
                fillBar = gameScreenUI.rootVisualElement.Q<VisualElement>("BarFill");
                waterTankLabel = gameScreenUI.rootVisualElement.Q<Label>("WaterTankLabel");
            }

            backgroundBar.style.backgroundColor = isExtinguishing ? backgroundColors[1] : backgroundColors[0];
            backgroundBar.style.backgroundColor = isIntaking ? backgroundColors[2] : backgroundBar.style.backgroundColor;
            float fillAmount = currentWater / maxWater;
            fillBar.style.width = Length.Percent(fillAmount * 100f);
            waterTankLabel.text = $"Water: % {fillAmount * 100:F1}";
        }
    }

    void StartExtinguishing()
    {
        if (currentWater > 0) isExtinguishing = true;
    }

    void StopExtinguishing()
    {
        isExtinguishing = false;
        wasLastExtinguish = true;
        ExtinguishAlongMovement(); // Son pozisyon için de hasar uygula
        wasLastExtinguish = false;
        hasLastPos = false;
    }

    // Dünya Vector3'ünü flat (top-down Euclidean) uzaya projekte eder.
    // Isometric Z As Y modunda Z, ekran Y'sine katkıda bulunur.
    Vector2 ToFlat(Vector3 w) =>
        new Vector2(w.x, (w.y + w.z * zToY) * yScale);

    // Kapsül içinde olup olmadığını test eder.
    // Nearest point hesabı flat uzayda, mesafe karşılaştırması dünya biriminde.
    bool IsInsideCapsule(Vector3 cellWorld, Vector2 p0f, Vector2 p1f)
    {
        Vector2 cf = ToFlat(cellWorld);

        Vector2 ab = p1f - p0f;
        float sqrLen = ab.sqrMagnitude;
        float t = sqrLen < Mathf.Epsilon ? 0f
                         : Mathf.Clamp01(Vector2.Dot(cf - p0f, ab) / sqrLen);
        Vector2 nearest = p0f + t * ab;

        Vector2 delta = cf - nearest;
        float dx = delta.x;
        float dy = delta.y / yScale; // Y'yi dünya birimine geri çevir

        return (dx * dx + dy * dy) <= (halfLength * halfLength);
    }

    // Flat uzaydaki nearest point'ten dünya birimi mesafesini döndürür.
    float IsoDistance(Vector3 cellWorld, Vector2 p0f, Vector2 p1f)
    {
        Vector2 cf = ToFlat(cellWorld);

        Vector2 ab = p1f - p0f;
        float sqrLen = ab.sqrMagnitude;
        float t = sqrLen < Mathf.Epsilon ? 0f
                         : Mathf.Clamp01(Vector2.Dot(cf - p0f, ab) / sqrLen);
        Vector2 nearest = p0f + t * ab;

        Vector2 delta = cf - nearest;
        float dx = delta.x;
        float dy = delta.y / yScale;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    void ExtinguishAlongMovement()
    {
        debugHits.Clear();

        if (!hasLastPos)
        {
            lastDropPos = waterDropPoint.position;
            hasLastPos = true;
            lastDropTime = Time.time;
            return;
        }

        Vector2 p0 = lastDropPos;
        Vector2 p1 = waterDropPoint.position;
        lastDropPos = p1;

        float segmentLength = Vector2.Distance(p0, p1);
        float passedTime = Time.time - lastDropTime;
        if ((segmentLength < 0.2f && passedTime < .1f) || wasLastExtinguish) return;
        lastDropTime = Time.time;

        // p0 ve p1'i flat uzaya çevir (Z=0 olduğu için zToY terimi sıfır)
        Vector2 p0f = ToFlat(new Vector3(p0.x, p0.y, 0f));
        Vector2 p1f = ToFlat(new Vector3(p1.x, p1.y, 0f));

        // Flat uzayda AABB (early reject için)
        float flatMinX = Mathf.Min(p0f.x, p1f.x) - halfLength;
        float flatMaxX = Mathf.Max(p0f.x, p1f.x) + halfLength;
        float flatMinY = Mathf.Min(p0f.y, p1f.y) - halfLength * yScale;
        float flatMaxY = Mathf.Max(p0f.y, p1f.y) + halfLength * yScale;

        // Tilemap'in tüm cell sınırlarını kullan — projeksiyon hatası olmaz
        BoundsInt bounds = groundTilemap.cellBounds;

        for (int x = bounds.xMin; x <= bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                Vector3 cellWorld = groundTilemap.GetCellCenterWorld(cell);
                Vector2 cellFlat = ToFlat(cellWorld);

                // Early reject
                if (cellFlat.x < flatMinX || cellFlat.x > flatMaxX ||
                    cellFlat.y < flatMinY || cellFlat.y > flatMaxY)
                    continue;

                if (!IsInsideCapsule(cellWorld, p0f, p1f)) continue;

                float d = IsoDistance(cellWorld, p0f, p1f);
                float weight = 1f - (d / halfLength);
                float damage = Mathf.Lerp(minDamage, maxDamage, weight);
                float delay = minExtinguishDelay + weight * maxExtraDelay;


                // damage = mesafeye bağlı factor * 


                _pendingExtinguishes.Add(new PendingExtinguish
                {
                    worldPos = groundTilemap.CellToWorld(cell),
                    timeToExecute = Time.time + delay,
                    damage = damage * passedTime * extinguishingPerSecond
                });

                debugHits.Add(new DebugWaterHit
                {
                    worldPos = cellWorld,
                    waterAmount = damage * passedTime *extinguishingPerSecond
                });
            }
        }

        HandlePendingExtinguishes();
    }

    void HandlePendingExtinguishes()
    {
        if (_pendingExtinguishes.Count == 0) return;

        // Sondan başa doğru gidiyoruz (silme işlemi güvenli olsun diye)
        for (int i = _pendingExtinguishes.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _pendingExtinguishes[i].timeToExecute)
            {
                // Zamanı geldi! Hasarı uygula
                fireManager.ApplyExtinguisherDamageToCell(
                    _pendingExtinguishes[i].worldPos,
                    _pendingExtinguishes[i].damage
                );

                // Listeden kaldır
                _pendingExtinguishes.RemoveAt(i);
            }
        }
    }

    public bool IsFullyOverWater()
    {
        Bounds b = intakeCollider.bounds;

        Vector3Int min = groundTilemap.WorldToCell(b.min);
        Vector3Int max = groundTilemap.WorldToCell(b.max);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector3Int cell = new(x, y, 0);
                CustomTile tile = groundTilemap.GetTile<CustomTile>(cell);

                if (tile == null || tile.biomeData == null || !tile.biomeData.isWater) return false;
            }
        }

        return true;
    }

    private void FillWater()
    {
        if (currentWater >= maxWater) return;

        currentWater += fillRate * Time.deltaTime;
        currentWater = Mathf.Min(currentWater, maxWater);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!hasLastPos) return;

        Vector2 p0 = lastDropPos;
        Vector2 p1 = waterDropPoint.position;

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawWireSphere(p0, halfLength);
        Gizmos.DrawWireSphere(p1, halfLength);

        foreach (var hit in debugHits)
        {
            float normalized = Mathf.Clamp01(hit.waterAmount / maxDamage);
            // 5f → test için max su referansı

            Gizmos.color = Color.Lerp(Color.yellow, Color.blue, normalized);

            Gizmos.DrawSphere(hit.worldPos, 0.15f);
        }
    }
}