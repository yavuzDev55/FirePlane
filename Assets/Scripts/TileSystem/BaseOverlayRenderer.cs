using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public abstract class BaseOverlayRenderer : MonoBehaviour
{
    [SerializeField] protected Material overlayMaterial;
    protected Material _instanceMat;
    protected MeshRenderer _mr;

    protected virtual void Awake()
    {
        _mr = GetComponent<MeshRenderer>();

        if (overlayMaterial == null)
        {
            Debug.LogError($"{name}: Material atanmadı!");
            return;
        }

        _instanceMat = new Material(overlayMaterial);
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows = false;
    }

    protected void InitMesh(Tilemap tilemap, int renderQueue)
    {
        _instanceMat.renderQueue = renderQueue;
        _mr.material = _instanceMat;

        BoundsInt bounds = tilemap.cellBounds;
        Grid grid = tilemap.layoutGrid;
        Vector3 cs = grid.cellSize;

        Vector3 worldMin = tilemap.CellToWorld(
            new Vector3Int(bounds.xMin, bounds.yMin, 0)
        );

        transform.position = new Vector3(
            worldMin.x,
            worldMin.y,
            tilemap.transform.position.z - 0.05f
        );

        GetComponent<MeshFilter>().mesh = BuildMesh(
            bounds.size.x, bounds.size.y, cs.x, cs.y
        );
    }

    static Mesh BuildMesh(float w, float h, float cellW, float cellH)
    {
        Mesh m = new Mesh { name = "OverlayMesh" };

        Vector3 v0 = new(0, 0, 0);
        Vector3 v1 = new(w * cellW * 0.5f, w * cellH * 0.5f, 0);
        Vector3 v2 = new((w - h) * cellW * 0.5f, (w + h) * cellH * 0.5f, 0);
        Vector3 v3 = new(-h * cellW * 0.5f, h * cellH * 0.5f, 0);

        m.vertices = new[] { v0, v1, v2, v3 };
        m.uv = new[] { new Vector2(0,0), new Vector2(1,0),
                              new Vector2(1,1), new Vector2(0,1) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };

        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void OnDestroy()
    {
        if (_instanceMat != null) Destroy(_instanceMat);
    }
}

