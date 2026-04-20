using System.Collections.Generic;
using UnityEngine;

public class ZoneTracker
{
    private FireManager fireManager;

    // Zone içindeki tüm hücreler
    public HashSet<Vector3Int> zoneCells { get; private set; }

    // Zone dışına çıkan, zone'a komşu hücreler — containment için
    public HashSet<Vector3Int> borderCells { get; private set; }

    // Toplam hücre sayısı
    public int TotalCells => zoneCells.Count;

    // Şu an yanan hücre sayısı (igniting + burning + raging)
    public int BurningCount { get; private set; }

    // Kül olan hücre sayısı
    public int AshCount { get; private set; }

    // Söndürülen hücre sayısı (wet state)
    public int ExtinguishedCount { get; private set; }

    // Zone sınırına geçen yanan hücre sayısı
    public int BorderBreachCount { get; private set; }

    // Yangın başladığında kaç hücre yanıyordu — ilerleme hesabı için
    public int InitialBurningCount { get; private set; }

    private bool initialized = false;

    private static readonly Vector3Int[] neighborOffsets = new Vector3Int[]
    {
        new( 1, 0, 0), new(-1, 0, 0),
        new( 0, 1, 0), new( 0,-1, 0),
        new( 1, 1, 0), new(-1,-1, 0),
        new( 1,-1, 0), new(-1, 1, 0)
    };

    public ZoneTracker(FireManager fm, HashSet<Vector3Int> cells)
    {
        fireManager = fm;
        zoneCells = cells;

        // Sınır hücrelerini hesapla
        borderCells = new HashSet<Vector3Int>();
        foreach (Vector3Int cell in zoneCells)
        {
            foreach (Vector3Int offset in neighborOffsets)
            {
                Vector3Int neighbor = cell + offset;
                if (!zoneCells.Contains(neighbor))
                    borderCells.Add(neighbor);
            }
        }
    }

    // Objective'ler Activate olunca çağrılır — başlangıç değerlerini kaydeder
    public void Initialize()
    {
        Refresh();
        InitialBurningCount = BurningCount;
        initialized = true;
    }

    // Her Tick'te çağrılır — tüm sayıları günceller
    public void Refresh()
    {
        BurningCount = 0;
        AshCount = 0;
        ExtinguishedCount = 0;
        BorderBreachCount = 0;

        foreach (Vector3Int cell in zoneCells)
        {
            FireCellData data = fireManager.GetCellData(cell);
            if (data == null) continue;

            switch (data.state)
            {
                case FireState.IGNITING:
                case FireState.BURNING:
                case FireState.RAGING:
                    BurningCount++;
                    break;

                case FireState.ASH:
                    AshCount++;
                    break;

                case FireState.WET:
                    ExtinguishedCount++;
                    break;
            }
        }

        // Sınır ihlali kontrolü
        foreach (Vector3Int cell in borderCells)
        {
            if (fireManager.IsCellBurning(cell))
                BorderBreachCount++;
        }
    }
}