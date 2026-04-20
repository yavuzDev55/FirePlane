using System.Collections.Generic;

public class MissionGroup : IMissionNode
{
    public string groupId;
    public bool isParallel;
    public int groupOrder;
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    public List<IMissionNode> nodes = new();
    private FireManager fireManager;
    private int currentIndex = 0;

    public void Activate(FireManager fm)
    {
        fireManager = fm;

        if (isParallel)
        {
            foreach (var node in nodes)
                node.Activate(fm);
        }
        else
        {
            if (nodes.Count > 0)
                nodes[0].Activate(fm);
        }
    }

    public void Tick()
    {
        if (IsCompleted || IsFailed) return;

        if (isParallel)
            TickParallel();
        else
            TickSequential();
    }

    void TickParallel()
    {
        foreach (var node in nodes)
        {
            if (!node.IsCompleted)
                node.Tick();

            // Herhangi biri başarısızsa grup da başarısız
            if (node.IsFailed)
            {
                IsFailed = true;
                IsCompleted = true;
                return;
            }
        }

        if (AreAllCompleted())
            IsCompleted = true;
    }

    void TickSequential()
    {
        if (currentIndex >= nodes.Count)
        {
            IsCompleted = true;
            return;
        }

        IMissionNode current = nodes[currentIndex];
        current.Tick();

        // Mevcut node başarısızsa grup da başarısız
        if (current.IsFailed)
        {
            IsFailed = true;
            IsCompleted = true;
            return;
        }

        if (current.IsCompleted)
        {
            currentIndex++;

            if (currentIndex < nodes.Count)
                nodes[currentIndex].Activate(fireManager);
        }

        if (currentIndex >= nodes.Count)
            IsCompleted = true;
    }

    public float GetProgress()
    {
        if (nodes.Count == 0) return 0f;

        float total = 0f;
        foreach (var node in nodes)
            total += node.GetProgress();

        return total / nodes.Count;
    }

    bool AreAllCompleted()
    {
        foreach (var node in nodes)
            if (!node.IsCompleted) return false;
        return true;
    }
}