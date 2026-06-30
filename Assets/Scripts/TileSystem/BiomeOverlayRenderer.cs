using UnityEngine;
using UnityEngine.Tilemaps;

public class BiomeOverlayRenderer : BaseOverlayRenderer
{
    static readonly int ID_BiomeTex = Shader.PropertyToID("_BiomeTex");
    static readonly int ID_BiomeIndexTex = Shader.PropertyToID("_BiomeIndexTex");  // YENİ
    static readonly int ID_TexSize = Shader.PropertyToID("_TexSize");

    public void Init(Texture2D biomeTex, Texture2D biomeIndexTex, Tilemap tilemap)  // YENİ parametre
    {
        InitMesh(tilemap, renderQueue: 3000);

        BoundsInt b = tilemap.cellBounds;
        _instanceMat.SetTexture(ID_BiomeTex, biomeTex);
        _instanceMat.SetTexture(ID_BiomeIndexTex, biomeIndexTex);  // YENİ
        _instanceMat.SetVector(ID_TexSize, new Vector4(b.size.x, b.size.y, 0, 0));
    }
}