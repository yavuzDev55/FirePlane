using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MissionManager : MonoBehaviour
{
    [Header("References")]
    public Tilemap missionZoneTilemap;
    public FireManager fireManager;

    [Header("Mission Groups")]
    // Editörden elle tanımlanan en üst seviye gruplar
    public List<MissionGroupData> rootGroups;

    [Header("Settings")]
    public float tickInterval = 0.5f;

    // Runtime'da oluşturulan grup ağacının kökleri
    private List<MissionGroup> runtimeGroups = new();

    // Tilemap'ten okunan zone hücreleri — MissionData başına
    private Dictionary<MissionData, HashSet<Vector3Int>> zoneMap = new();

    private float tickTimer;

    public event System.Action OnMissionsActivated;

    [Header("Visual")]
    public TilemapTextureBuilder textureBuilder;
    public MissionOverlayRenderer missionOverlay;

    void Start()
    {
        ReadZoneMap();
        BuildRuntimeGroups();
        fireManager.OnFireInitialized += ActivateGroups;
    }

    void Update()
    {
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            TickAll();
            tickTimer = 0f;
        }
    }

    // Tilemap'i tarayıp her MissionData için zone hücrelerini topla
    void ReadZoneMap()
    {
        BoundsInt bounds = missionZoneTilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                CustomTile tile = missionZoneTilemap.GetTile<CustomTile>(cell);

                if (tile == null || tile.missionData == null) continue;

                if (!zoneMap.ContainsKey(tile.missionData))
                    zoneMap[tile.missionData] = new HashSet<Vector3Int>();

                zoneMap[tile.missionData].Add(cell);
            }
        }
    }

    // MissionGroupData asset'lerinden runtime MissionGroup ağacını kur
    void BuildRuntimeGroups()
    {
        foreach (var groupData in rootGroups)
        {
            MissionGroup group = BuildGroup(groupData);
            runtimeGroups.Add(group);
        }

        // groupOrder'a göre sırala
        runtimeGroups.Sort((a, b) => a.groupOrder.CompareTo(b.groupOrder));
    }

    // Özyinelemeli — içinde grup olan grupları da doğru kurar
    MissionGroup BuildGroup(MissionGroupData groupData)
    {
        MissionGroup group = new MissionGroup
        {
            groupId = groupData.groupId,
            isParallel = groupData.isParallel,
            groupOrder = groupData.groupOrder
        };

        foreach (var nodeData in groupData.nodes)
        {
            if (nodeData.nodeType == MissionNodeType.Objective)
            {
                // Yaprak node — MissionObjective oluştur
                MissionObjective objective = CreateObjective(nodeData.missionData);
                if (objective != null)
                    group.nodes.Add(objective);
            }
            else if (nodeData.nodeType == MissionNodeType.Group)
            {
                // Dal node — özyinelemeli olarak iç grubu kur
                MissionGroup innerGroup = BuildGroup(nodeData.groupData);
                group.nodes.Add(innerGroup);
            }
        }

        return group;
    }

    // MissionData'ya göre doğru objective tipini oluştur
    MissionObjective CreateObjective(MissionData data)
    {
        if (data == null) return null;

        MissionObjective objective = data.missionType switch
        {
            MissionType.ExtinguishZone => new ExtinguishZoneMission(),
            MissionType.ContainmentZone => new ContainmentZoneMission(),
            _ => null
        };

        if (objective == null) return null;

        objective.missionData = data;

        // Zone hücrelerini tilemap'ten oku
        if (zoneMap.TryGetValue(data, out var cells))
            objective.zoneCells = cells;
        else
            Debug.LogWarning($"Zone hücresi bulunamadı: {data.missionName}");
        
        return objective;
    }

    void ActivateGroups()
    {
        fireManager.OnFireInitialized -= ActivateGroups;

        foreach (var group in runtimeGroups)
            group.Activate(fireManager);

        OnMissionsActivated?.Invoke();
    }

    void TickAll()
    {
        foreach (var group in runtimeGroups)
        {
            if (group.IsCompleted) continue;

            group.Tick();

            // Grup içindeki node'ları kontrol et
            CheckGroupNodes(group);
        }
    }

    void CheckGroupNodes(MissionGroup group)
    {
        foreach (var node in group.nodes)
        {
            if (node is MissionObjective objective)
            {
                if (objective.IsCompleted && !objective.IsFailed)
                    OnObjectiveCompleted(objective.missionData);

                else if (objective.IsFailed)
                    OnObjectiveFailed(objective.missionData);
            }
            else if (node is MissionGroup innerGroup)
            {
                CheckGroupNodes(innerGroup); // iç gruplar için özyinelemeli
            }
        }
    }

    public List<MissionGroup> GetRuntimeGroups() => runtimeGroups;

    void OnObjectiveCompleted(MissionData mission)
    {
        textureBuilder.UpdateMissionState(mission, MissionVisualState.Completed);
        missionOverlay.UpdateTexture(textureBuilder.MissionTexture);
    }

    void OnObjectiveFailed(MissionData mission)
    {
        textureBuilder.UpdateMissionState(mission, MissionVisualState.Failed);
        missionOverlay.UpdateTexture(textureBuilder.MissionTexture);
    }
}