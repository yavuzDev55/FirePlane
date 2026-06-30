using UnityEngine;
using UnityEditor;

public class BiomeTextureArrayBuilder : EditorWindow
{
    [MenuItem("Tools/Build Biome Texture Array")]
    static void Build()
    {
        Texture2D[] textures = new Texture2D[]
        {
            // SIRA = CustomTile'daki textureIndex ile eşleşmeli
            Load("Assets/Textures/Biomes/grassland.png"),  // index 0
            Load("Assets/Textures/Biomes/forest.png"),     // index 1
            Load("Assets/Textures/Biomes/dryforest.png"),  // index 2
            Load("Assets/Textures/Biomes/rock.png"),       // index 3
            Load("Assets/Textures/Biomes/water.png"),      // index 4
        };

        // Null kontrolü
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] == null)
            {
                Debug.LogError($"Texture {i} bulunamadı! Yolu kontrol et.");
                return;
            }
        }

        int width = textures[0].width;
        int height = textures[0].height;
        int count = textures.Length;

        Debug.Log($"Boyut: {width}x{height}, Slice: {count}");

        // Boyut kontrolü
        for (int i = 1; i < count; i++)
        {
            if (textures[i].width != width || textures[i].height != height)
            {
                Debug.LogError($"Texture {i} boyutu uyuşmuyor! " +
                               $"Beklenen: {width}x{height}, " +
                               $"Gelen: {textures[i].width}x{textures[i].height}");
                return;
            }
        }

        // Texture Array oluştur
        Texture2DArray array = new Texture2DArray(
            width, height, count,
            TextureFormat.RGBA32,
            mipChain: true
        );

        array.filterMode = FilterMode.Bilinear;
        array.wrapMode = TextureWrapMode.Repeat;

        for (int i = 0; i < count; i++) {
            Color[] pixels = textures[i].GetPixels();
            array.SetPixels(pixels, i, 0);
        }
        

        array.Apply();

        string savePath = "Assets/Textures/Biomes/BiomeTextureArray.asset";
        AssetDatabase.CreateAsset(array, savePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Texture Array kaydedildi: {savePath}");
    }

    static Texture2D Load(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
            Debug.LogWarning($"Yüklenemedi: {path}");
        return tex;
    }
}