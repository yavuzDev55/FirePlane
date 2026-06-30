using UnityEngine;
using UnityEngine.Tilemaps;

public class MissionOverlayRenderer : BaseOverlayRenderer
{
    static readonly int ID_MissionTex = Shader.PropertyToID("_MissionTex");
    static readonly int ID_TexSize = Shader.PropertyToID("_TexSize");

    public void Init(Texture2D missionTex, Tilemap tilemap)
    {
        InitMesh(tilemap, renderQueue: 3020);

        BoundsInt b = tilemap.cellBounds;
        _instanceMat.SetTexture(ID_MissionTex, missionTex);
        _instanceMat.SetVector(ID_TexSize,
            new Vector4(b.size.x, b.size.y, 0, 0));
    }

    public void UpdateTexture(Texture2D missionTex)
    {
        _instanceMat.SetTexture(ID_MissionTex, missionTex);
    }
}