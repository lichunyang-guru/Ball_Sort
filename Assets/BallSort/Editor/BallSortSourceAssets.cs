#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Collections;
using UnityEngine.U2D;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Recreates the original exported asset IDs so the scene importer can restore references.</summary>
public static class BallSortSourceAssets
{
    const string Root = "Assets/BallSort/Generated";
    public static Dictionary<int, Object> Build(JObject data)
    {
        Directory.CreateDirectory(Root + "/Sprites");
        Directory.CreateDirectory(Root + "/Animations");
        AssetDatabase.Refresh();
        var map = new Dictionary<int, Object>();
        foreach (var item in data["textures"])
        {
            int id = (int)item["id"];
            string path = "Assets/BallSort/Art/Textures/" + id + ".png";
            if (!File.Exists(path)) path = Path.ChangeExtension(path, ".jpeg");
            if (!File.Exists(path)) path = Path.ChangeExtension(path, ".jpg");
            if (!File.Exists(path)) { Debug.LogWarning("Missing extracted texture " + id); continue; }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = (FilterMode)((int?)item["filterMode"] ?? 1);
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            map[id] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        foreach (var item in data["sprites"])
        {
            int id = (int)item["id"], textureId = (int)item["texture"]["$ref"];
            if (!map.TryGetValue(textureId, out var asset)) continue;
            Texture2D source = (Texture2D)asset;
            int width = Mathf.RoundToInt((float)item["nativeSize"]["x"]);
            int height = Mathf.RoundToInt((float)item["nativeSize"]["y"]);
            if (width <= 0 || height <= 0) continue;
            // A packed atlas removes transparent margins. Rebuild the original native rectangle,
            // rather than extending its UV rect into neighbouring sprites or changing its pivot.
            var packed = item["packedRect"];
            int sx = Mathf.RoundToInt((float)packed["x"] * source.width);
            int sy = Mathf.RoundToInt((float)packed["y"] * source.height);
            int sw = Mathf.RoundToInt((float)packed["width"] * source.width);
            int sh = Mathf.RoundToInt((float)packed["height"] * source.height);
            int dx = Mathf.RoundToInt((float)item["textureRectOffset"]["x"]);
            int dy = Mathf.RoundToInt((float)item["textureRectOffset"]["y"]);
            // Texture overrides may have a different resolution from the source metadata.
            float rx = source.width / (float)item["textureWidth"];
            float ry = source.height / (float)item["textureHeight"];
            int dw = Mathf.Max(1, Mathf.RoundToInt(sw / rx));
            int dh = Mathf.Max(1, Mathf.RoundToInt(sh / ry));
            var pixels = new Color32[width * height];
            var sourcePixels = source.GetPixels32();
            for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                int tx = dx + x, ty = dy + y;
                int px = sx + Mathf.Clamp(Mathf.FloorToInt((x + .5f) * sw / dw), 0, sw - 1);
                int py = sy + Mathf.Clamp(Mathf.FloorToInt((y + .5f) * sh / dh), 0, sh - 1);
                if (tx >= 0 && tx < width && ty >= 0 && ty < height && px >= 0 && px < source.width && py >= 0 && py < source.height)
                    pixels[ty * width + tx] = sourcePixels[py * source.width + px];
            }
            var restored = new Texture2D(width, height, TextureFormat.RGBA32, false);
            restored.SetPixels32(pixels); restored.Apply();
            string pngPath = Root + "/Sprites/" + id + ".png";
            File.WriteAllBytes(pngPath, restored.EncodeToPNG());
            Object.DestroyImmediate(restored);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            var si = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            si.textureType = TextureImporterType.Sprite;
            si.spriteImportMode = SpriteImportMode.Single;
            si.spritePixelsPerUnit = (float)item["pixelsPerUnit"];
            var settings = new TextureImporterSettings(); si.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2((float)item["pivot"]["x"] / width, (float)item["pivot"]["y"] / height);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = Vec4(item["border"]);
            si.SetTextureSettings(settings);
            si.mipmapEnabled = false; si.isReadable = true;
            si.alphaSource = TextureImporterAlphaSource.FromInput; si.alphaIsTransparency = true;
            si.textureCompression = TextureImporterCompression.Uncompressed;
            si.maxTextureSize = 8192; si.npotScale = TextureImporterNPOTScale.None;
            si.SaveAndReimport();
            // Keep the exported tight mesh as well as the transparent native rectangle.
            // Bounds are used by tube completion alignment and portrait framing.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            float ppu = (float)item["pixelsPerUnit"];
            Vector2 pixelPivot = new Vector2((float)item["pivot"]["x"], (float)item["pivot"]["y"]);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pixelPivot.x / width, pixelPivot.y / height), ppu, 0, SpriteMeshType.FullRect, Vec4(item["border"]));
            sprite.name = (string)item["name"];
            ApplySourceGeometry(sprite, item);
            Save(sprite, Root + "/Sprites/" + id + ".asset");
            map[id] = sprite;
        }
        foreach (var item in data["sounds"])
        {
            string name = (string)item["data"][0];
            map[(int)item["id"]] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/BallSort/Audio/" + name + ".mp3");
        }
        foreach (var item in data["materials"])
        {
            int id = (int)item["id"];
            var material = new Material(Shader.Find("UI/Default")) { name = (string)item["name"] };
            foreach (var p in item["floatParameters"]) if (material.HasProperty((string)p["name"])) material.SetFloat((string)p["name"], (float)p["value"]);
            foreach (var p in item["colorParameters"]) if (material.HasProperty((string)p["name"])) material.SetColor((string)p["name"], ColorValue(p["value"]));
            material.renderQueue = (int)item["renderQueue"];
            Save(material, Root + "/Material_" + id + ".mat"); map[id] = material;
        }
        var defaultSprite = new Material(Shader.Find("Sprites/Default")) { name = "Source Default Sprite Material" };
        Save(defaultSprite, Root + "/DefaultSprite.mat"); map[724] = defaultSprite;
        map[780] = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (var item in data["scriptableObjects"])
        {
            if ((string)item["type"] != "BallVisualPalette") continue;
            var palette = ScriptableObject.CreateInstance<BallVisualPalette>();
            var serialized = new SerializedObject(palette);
            var entries = serialized.FindProperty("entries"); var sourceEntries = (JArray)item["fields"]["entries"];
            entries.arraySize = sourceEntries.Count;
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                var p = entries.GetArrayElementAtIndex(i); var sourceEntry = sourceEntries[i];
                p.FindPropertyRelative("color").intValue = (int)sourceEntry["color"];
                p.FindPropertyRelative("sprite").objectReferenceValue = Ref(map, sourceEntry["sprite"]);
                p.FindPropertyRelative("lazhuSprite").objectReferenceValue = Ref(map, sourceEntry["lazhuSprite"]);
                p.FindPropertyRelative("tint").colorValue = ColorValue(sourceEntry["tint"]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Save(palette, Root + "/BallVisualPalette.asset"); map[(int)item["id"]] = palette;
        }
        foreach (var item in data["animationClips"])
        {
            var raw = item["data"]; var clip = new AnimationClip { name = (string)raw[0], frameRate = (float)raw[7] };
            foreach (var curve in raw[4])
            {
                string type = (string)curve[2];
                if (((JArray)curve[5]).Count == 0) continue;
                var keys = new List<ObjectReferenceKeyframe>();
                foreach (var key in curve[5])
                    keys.Add(new ObjectReferenceKeyframe { time = (float)key[0], value = map.TryGetValue((int)key[2], out var v) ? v : null });
                var binding = EditorCurveBinding.PPtrCurve((string)curve[0], type == "UnityEngine.SpriteRenderer" ? typeof(SpriteRenderer) : typeof(Transform), (string)curve[3]);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            }
            if ((int)item["id"] == 28492)
            {
                // Exact half-float key data from bundle -1/data.blob bytes 0..83.
                // Each exported key has seven values: time,value,tangents,weight data.
                float[] start = { .89892578125f, .91552734375f, 1.25f };
                for (int i = 0; i < 3; i++)
                {
                    var curve = new AnimationCurve(new Keyframe(1.2998046875f, start[i], 0, 0), new Keyframe(1.31640625f, i == 2 ? 1.25f : 0, 0, 0));
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale." + "xyz"[i]), curve);
                }
            }
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = (int)raw[2] != 0;
            clipSettings.stopTime = (float)raw[3];
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            Save(clip, Root + "/Animations/" + item["id"] + ".anim"); map[(int)item["id"]] = clip;
        }
        foreach (var item in data["animatorControllers"])
        {
            string path = Root + "/Animations/" + item["id"] + ".controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.name = (string)item["data"][0];
            var rawLayer = item["data"][1][0]; var machine = rawLayer[7];
            foreach (var rawState in machine[3])
            {
                var state = controller.layers[0].stateMachine.AddState((string)rawState[1]);
                state.motion = map.TryGetValue((int)rawState[8], out var motion) ? motion as Motion : null;
                state.speed = (float)rawState[11]; state.writeDefaultValues = (int)rawState[16] != 0;
                if ((int)rawState[0] == (int)machine[8]) controller.layers[0].stateMachine.defaultState = state;
            }
            map[(int)item["id"]] = controller;
        }
        AssetDatabase.SaveAssets();
        return map;
    }
    /// <summary>Loads a previously completed Build without importing, generating, or modifying any asset.</summary>
    /// <exception cref="FileNotFoundException">A required generated asset is absent; run Build explicitly.</exception>
    public static Dictionary<int, Object> LoadExisting(JObject data)
    {
        var map = new Dictionary<int, Object>();
        foreach (var item in data["textures"])
        {
            int id = (int)item["id"];
            string path = "Assets/BallSort/Art/Textures/" + id + ".png";
            if (!File.Exists(path)) path = Path.ChangeExtension(path, ".jpeg");
            if (!File.Exists(path)) path = Path.ChangeExtension(path, ".jpg");
            map[id] = RequireExisting<Texture2D>(path);
        }
        foreach (var item in data["sprites"])
            map[(int)item["id"]] = RequireExisting<Sprite>(Root + "/Sprites/" + item["id"] + ".asset");
        foreach (var item in data["sounds"])
            map[(int)item["id"]] = RequireExisting<AudioClip>("Assets/BallSort/Audio/" + item["data"][0] + ".mp3");
        foreach (var item in data["materials"])
            map[(int)item["id"]] = RequireExisting<Material>(Root + "/Material_" + item["id"] + ".mat");
        map[724] = RequireExisting<Material>(Root + "/DefaultSprite.mat");
        map[780] = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (var item in data["scriptableObjects"])
            if ((string)item["type"] == "BallVisualPalette")
                map[(int)item["id"]] = RequireExisting<BallVisualPalette>(Root + "/BallVisualPalette.asset");
        foreach (var item in data["animationClips"])
            map[(int)item["id"]] = RequireExisting<AnimationClip>(Root + "/Animations/" + item["id"] + ".anim");
        foreach (var item in data["animatorControllers"])
            map[(int)item["id"]] = RequireExisting<AnimatorController>(Root + "/Animations/" + item["id"] + ".controller");
        return map;
    }
    /// <summary>Repairs mesh data in already generated sprites without reimporting textures.</summary>
    public static void RepairExistingGeometry(JObject data)
    {
        foreach (var item in data["sprites"])
        {
            var sprite = RequireExisting<Sprite>(Root + "/Sprites/" + item["id"] + ".asset");
            ApplySourceGeometry(sprite, item);
            EditorUtility.SetDirty(sprite);
        }
        AssetDatabase.SaveAssets();
    }
    static void ApplySourceGeometry(Sprite sprite, JToken item)
    {
        // OverrideGeometry is restricted outside the player loop in this Editor.
        // Use the same mesh-data API as Unity's com.unity.2d.animation SpritePostProcess.
        // SetVertexAttribute(Position) takes local units; TexCoord0 takes normalized UVs.
        var sourceVertices = (JArray)item["vertices"];
        var sourceTriangles = (JArray)item["triangles"];
        int count = sourceVertices.Count / 5;
        if (count == 0 || sourceTriangles.Count == 0) return;
        float width = (float)item["nativeSize"]["x"], height = (float)item["nativeSize"]["y"];
        float ppu = (float)item["pixelsPerUnit"];
        var pivot = new Vector2((float)item["pivot"]["x"], (float)item["pivot"]["y"]);
        var positions = new NativeArray<Vector3>(count, Allocator.Temp);
        var uvs = new NativeArray<Vector2>(count, Allocator.Temp);
        var indices = new NativeArray<ushort>(sourceTriangles.Count, Allocator.Temp);
        try
        {
            for (int i = 0; i < count; i++)
            {
                float x = (float)sourceVertices[i * 5], y = (float)sourceVertices[i * 5 + 1];
                positions[i] = new Vector3(x, y, (float)sourceVertices[i * 5 + 2]);
                uvs[i] = new Vector2((x * ppu + pivot.x) / width, (y * ppu + pivot.y) / height);
            }
            for (int i = 0; i < indices.Length; i++) indices[i] = (ushort)sourceTriangles[i];
            sprite.SetVertexCount(count);
            sprite.SetVertexAttribute<Vector3>(VertexAttribute.Position, positions);
            sprite.SetVertexAttribute<Vector2>(VertexAttribute.TexCoord0, uvs);
            sprite.SetIndices(indices);
        }
        finally { positions.Dispose(); uvs.Dispose(); indices.Dispose(); }
    }
    static T RequireExisting<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) throw new FileNotFoundException("BallSort generated asset is missing. Run the full asset Build first: " + path, path);
        return asset;
    }
    static Object Ref(Dictionary<int, Object> map, JToken token) => token is JObject obj && obj["$ref"] != null && map.TryGetValue((int)obj["$ref"], out var value) ? value : null;
    static Color ColorValue(JToken v) => new Color((float)v["r"], (float)v["g"], (float)v["b"], (float)v["a"]);
    static Vector4 Vec4(JToken v) => new Vector4((float)v["x"], (float)v["y"], (float)v["z"], (float)v["w"]);
    static void Save(Object value, string path) { AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(value, path); }
}
#endif
