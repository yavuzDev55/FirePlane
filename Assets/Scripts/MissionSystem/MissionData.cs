using UnityEngine;

[CreateAssetMenu(menuName = "Missions/MissionData")]
public class MissionData : ScriptableObject
{
    [Header("Info")]
    public string missionName;
    [TextArea] public string description;
    public MissionType missionType;
}

public enum MissionType { ExtinguishZone, ContainmentZone }