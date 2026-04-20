using System.Collections.Generic;
using UnityEngine;

public abstract class MissionObjective : IMissionNode
{
    public MissionData missionData;
    public HashSet<Vector3Int> zoneCells;

    // Görev tamamlandı mı — başarı veya başarısızlık sonucu kapanır
    public bool IsCompleted { get; protected set; }

    // Görev başarısız mı — IsCompleted true olduğunda buna bakılır
    public bool IsFailed { get; protected set; }

    protected FireManager fireManager;
    protected ZoneTracker zoneTracker;

    public virtual void Activate(FireManager fm)
    {
        fireManager = fm;
        IsCompleted = false;
        IsFailed = false;

        zoneTracker = new ZoneTracker(fm, zoneCells);
        zoneTracker.Initialize();
    }

    public abstract void Tick();
    public abstract float GetProgress();
}