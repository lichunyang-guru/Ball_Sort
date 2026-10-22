#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Stores the source mesh on the PNG importer, so no standalone Sprite asset is needed.</summary>
public static class BallSortPngSpriteData
{
    public static Sprite Write(string path, float ppu, Vector2 pivot, Vector4 border,
        Vector2[] localVertices, ushort[] triangles)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing PNG importer: " + path);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(pivot.x / texture.width, pivot.y / texture.height);
        settings.spriteBorder = border;
        importer.SetTextureSettings(settings);

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("No Sprite data provider: " + path);
        provider.InitSpriteEditorDataProvider();
        var rects = provider.GetSpriteRects();
        if (rects.Length != 1 || !provider.HasDataProvider(typeof(ISpriteMeshDataProvider)))
            throw new InvalidOperationException("Expected one editable Sprite mesh: " + path);
        rects[0].alignment = SpriteAlignment.Custom;
        rects[0].pivot = settings.spritePivot;
        rects[0].border = border;
        provider.SetSpriteRects(rects);
        var mesh = provider.GetDataProvider<ISpriteMeshDataProvider>();
        mesh.SetVertices(rects[0].spriteID, localVertices.Select(v => new Vertex2DMetaData
            { position = v * ppu + pivot }).ToArray());
        mesh.SetIndices(rects[0].spriteID, triangles.Select(i => (int)i).ToArray());
        mesh.SetEdges(rects[0].spriteID, new Vector2Int[0]);
        provider.Apply();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
