using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class FireOverlayRenderer : MonoBehaviour
{
    static readonly int ID_StateTex = Shader.PropertyToID("_StateTex");
    static readonly int ID_TexSize = Shader.PropertyToID("_TexSize");

    // [SerializeField] ekleyerek materyali dışarıdan sürükleyip bırakılabilir yapıyoruz
    [SerializeField] private Material fireMaterial;

    MeshRenderer _mr;
    Material _instanceMat;

    void Awake()
    {
        _mr = GetComponent<MeshRenderer>();

        if (fireMaterial == null)
        {
            Debug.LogError("Lütfen Inspector üzerinden bir Material atayın!");
            return;
        }

        // Projedeki orijinal materyali bozmamak için bir kopyasını (instance) oluşturuyoruz
        _instanceMat = new Material(fireMaterial);
        _instanceMat.renderQueue = 3010; // Tilemap'in hemen önü

        _mr.material = _instanceMat;
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows = false;
    }

    public void Init(Texture2D stateTex, Tilemap tilemap)
    {
        if (_instanceMat == null) return;

        BoundsInt bounds = tilemap.cellBounds;
        Grid grid = tilemap.layoutGrid;
        Vector3 cs = grid.cellSize;

        Vector3 worldMin = tilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));

        transform.position = new Vector3(worldMin.x, worldMin.y, tilemap.transform.position.z - 0.05f);
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;

        GetComponent<MeshFilter>().mesh = BuildIsometricMesh(bounds.size.x, bounds.size.y, cs.x, cs.y);

        _instanceMat.SetTexture(ID_StateTex, stateTex);
        _instanceMat.SetVector(ID_TexSize, new Vector4(bounds.size.x, bounds.size.y, 0, 0));
    }

    static Mesh BuildIsometricMesh(float widthCells, float heightCells, float cellW, float cellH)
    {
        Mesh m = new Mesh { name = "IsometricOverlayQuad" };

        Vector3 v0 = new Vector3(0, 0, 0);
        Vector3 v1 = new Vector3(widthCells * cellW * 0.5f, widthCells * cellH * 0.5f, 0);
        Vector3 v2 = new Vector3((widthCells - heightCells) * cellW * 0.5f, (widthCells + heightCells) * cellH * 0.5f, 0);
        Vector3 v3 = new Vector3(-heightCells * cellW * 0.5f, heightCells * cellH * 0.5f, 0);

        m.vertices = new[] { v0, v1, v2, v3 };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };

        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void OnDestroy() { if (_instanceMat != null) Destroy(_instanceMat); }
}