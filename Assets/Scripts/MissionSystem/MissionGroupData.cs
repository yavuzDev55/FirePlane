using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Missions/MissionGroupData")]
public class MissionGroupData : ScriptableObject
{
    public string groupId;
    public bool isParallel;
    public int groupOrder;

    // İçine hem MissionData hem de başka MissionGroupData koyulabilir
    public List<MissionNodeData> nodes;
}

// MissionData mı MissionGroupData mı olduğunu editörden seçmek için
[System.Serializable]
public class MissionNodeData
{
    public MissionNodeType nodeType;
    public MissionData missionData;         // nodeType == Objective ise
    public MissionGroupData groupData;      // nodeType == Group ise
}

public enum MissionNodeType { Objective, Group }