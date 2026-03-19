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

    private void Awake()
    {
        if (fireManager == null)
        {
            fireManager = FindAnyObjectByType<FireManager>();
        }

        cellPos = fireManager.groundTilemap.WorldToCell(transform.position);

        fireManager.RegisterFlammable(cellPos, this);
    }

    public void Ignite()
    {
        if (!IsBurning)
        {
            IsBurning = true;

            if (fireEffectPrefab != null && currentFireEffect == null)
            {
                currentFireEffect = Instantiate(
                    fireEffectPrefab,
                    transform.position + Vector3.up * 0.2f,
                    Quaternion.identity,
                    transform
                );
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

    private void OnDestroy()
    {
        if (fireManager != null)
        {
            fireManager.UnregisterFlammable(cellPos, this);
        }
    }
}