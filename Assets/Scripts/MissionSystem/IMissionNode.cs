public interface IMissionNode
{
    bool IsCompleted { get; }
    bool IsFailed { get; }
    void Activate(FireManager fireManager);
    void Tick();
    float GetProgress();
}