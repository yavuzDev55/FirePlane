public class ContainmentZoneMission : MissionObjective
{
    public override void Tick()
    {
        if (IsCompleted) return;

        zoneTracker.Refresh();

        if (zoneTracker.BorderBreachCount > 0)
        {
            IsFailed = true;
            IsCompleted = true;
        }
    }

    public override float GetProgress()
    {
        return IsFailed ? 0f : 1f;
    }
}