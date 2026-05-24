using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TileVisualCoordinator : MonoBehaviour
{
    [Header("References")]
    public FireManager fireManager;
    public Tilemap groundTilemap;
    public FireOverlayRenderer overlayRenderer;

    private Texture2D _stateTexture;
    private Color[] _pixels;

    private int _texWidth;
    private int _texHeight;
    private int _offsetX;
    private int _offsetY;

    private HashSet<Vector3Int> _dirtyQueue = new();

    static readonly float[] StateToB = { 0.00f, 0.20f, 0.40f, 0.60f, 0.80f, 1.00f };

    void Awake()
    {
        BoundsInt bounds = groundTilemap.cellBounds;

        _offsetX = bounds.xMin;
        _offsetY = bounds.yMin;
        _texWidth = bounds.size.x;
        _texHeight = bounds.size.y;

        _pixels = new Color[_texWidth * _texHeight];

        for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                if (groundTilemap.GetTile(cell) != null)
                    _pixels[CellToIndex(cell)].a = 1f;
            }

        _stateTexture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "StateTexture"
        };

        _stateTexture.SetPixels(_pixels);
        _stateTexture.Apply(false);

        overlayRenderer.Init(_stateTexture, groundTilemap);
    }

    public void MarkDirty(Vector3Int cellPos) => _dirtyQueue.Add(cellPos);

    void LateUpdate()
    {
        if (_dirtyQueue.Count == 0) return;

        // Döngü sırasında HashSet içerisinden eleman silmek hata (InvalidOperationException) vereceği için
        // İşlemi biten (normale dönen) hücreleri bu geçici listede toplayacağız
        List<Vector3Int> cellsToRemove = new List<Vector3Int>();

        foreach (var cell in _dirtyQueue)
        {
            int idx = CellToIndex(cell);
            if (idx < 0) continue;

            FireCellData data = fireManager.GetCellData(cell);

            // KONTROL: Eğer veri yoksa veya hücre tamamen normale dönmüşse (ısı bitti, nem kurudu, state NORMAL)
            if (data == null || (data.heat <= 0.01f && data.wetness <= 0.01f && data.state == FireState.NORMAL))
            {
                // Shader'da bu pikseli tamamen sıfırla (Şeffaf çimen görünümü)
                _pixels[idx] = new Color(0f, 0f, StateToB[0], _pixels[idx].a);
                cellsToRemove.Add(cell); // Aktif takipten çıkarılmak üzere işaretle
            }
            else
            {
                // AKTİF DURUM: Hücre yanıyor veya nemliyse güncel ısı ve her kare değişen nemi shader'a gönder!
                _pixels[idx] = new Color(data.heat, data.wetness, StateToB[(int)data.state], _pixels[idx].a);
            }
        }

        // İşlemi biten normalleşmiş hücreleri kuyruktan temizle
        foreach (var cell in cellsToRemove)
        {
            _dirtyQueue.Remove(cell);
        }

        // Dokuyu güncelle ve ekran kartına yolla
        _stateTexture.SetPixels(_pixels);
        _stateTexture.Apply(false);

        // DİKKAT: Eski kodun en altında yer alan "_dirtyQueue.Clear();" satırını SİLDİK.
        // Artık kuyruk kendi kendini yukarıdaki cellsToRemove ile dinamik olarak temizliyor.
    }

    int CellToIndex(Vector3Int cell)
    {
        int x = cell.x - _offsetX;
        int y = cell.y - _offsetY;
        if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight) return -1;
        return y * _texWidth + x;
    }

    void OnDestroy()
    {
        if (_stateTexture != null) Destroy(_stateTexture);
    }
}