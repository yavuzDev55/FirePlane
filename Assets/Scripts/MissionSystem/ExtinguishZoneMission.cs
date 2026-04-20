public class ExtinguishZoneMission : MissionObjective
{
    public override void Tick()
    {
        if (IsCompleted) return;

        zoneTracker.Refresh();

        if (zoneTracker.BurningCount == 0)
            IsCompleted = true;
    }

    public override float GetProgress()
    {
        if (zoneTracker.InitialBurningCount == 0) return 1f;

        return 1f - ((float)zoneTracker.BurningCount / zoneTracker.InitialBurningCount);
    }
}