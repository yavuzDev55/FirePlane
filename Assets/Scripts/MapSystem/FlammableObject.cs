using UnityEngine;
using UnityEngine.Tilemaps;

public class FlammableObject : MonoBehaviour
{
    [Header("References")]
    public FireManager fireManager; 
    public GameObject fireEffectPrefab; 
    private GameObject currentFireEffect;

    private Vector3Int cellPos;

    [Header("State")]
    public bool IsBurning { get; private set; } = false;

    void Start()
    {
        fireManager = Object.FindAnyObjectByType<FireManager>();;

        Tilemap groundMap = fireManager.groundTilemap;
        cellPos = groundMap.WorldToCell(transform.position);

        fireManager.RegisterFlammable(cellPos, this);
    }

    
    // --- GÖRSEL METOTLAR ---

    public void Ignite()
    {
        if (!IsBurning)
        {
            IsBurning = true;
            if(fireEffectPrefab != null && currentFireEffect == null)
            {
                // Efekti nesnenin üzerinde spawn et
                currentFireEffect = Instantiate(fireEffectPrefab, transform.position + Vector3.up * 0.2f, Quaternion.identity, transform);
            }
        }
    }
    
    public void Extinguish()
    {
        if (IsBurning)
        {
            IsBurning = false;
            
            if (currentFireEffect != null)
            {
                Destroy(currentFireEffect);
                currentFireEffect = null;
            }
        }
    }
    
}