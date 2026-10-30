#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
public static class InspectSceneSprites
{
    [MenuItem("Tools/BallSort/Inspect Scene Sprites")]
    static void Run()
    {
        var report = new StringBuilder();
        foreach (SceneView view in SceneView.sceneViews)
            report.AppendLine("SceneView mode=" + view.cameraMode.drawMode + " lighting=" + view.sceneLighting + " 2D=" + view.in2DMode + " mask=" + view.camera.cullingMask + " camera=" + view.camera.transform.position + " near=" + view.camera.nearClipPlane + " far=" + view.camera.farClipPlane);
        var root = Selection.activeGameObject;
        if (root && root.GetComponentInParent<TubeView>()) root = root.GetComponentInParent<TubeView>().gameObject;
        if (!root) root = GameObject.Find("Tubes");
        report.AppendLine("Selected=" + (root ? root.name : "null"));
        if (root) foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var sprite=renderer.sprite;
            var material = renderer.sharedMaterial;
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            report.AppendLine("material=" + (material ? material.name : "null") + " color=" + (material && material.HasProperty("_Color") ? material.GetColor("_Color").ToString() : "none") + " blockEmpty=" + block.isEmpty + " blockColor=" + block.GetColor("_Color") + " shaderSupported=" + (material && material.shader.isSupported));
            if (sprite) report.AppendLine("uv=" + string.Join(",", sprite.uv) + " tex=" + sprite.texture.width + "x" + sprite.texture.height + " rect=" + sprite.rect);

            report.AppendLine(renderer.name + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled + " forceOff=" + renderer.forceRenderingOff + " hidden=" + SceneVisibilityManager.instance.IsHidden(renderer.gameObject) + " color=" + renderer.color + " pos=" + renderer.transform.position + " scale=" + renderer.transform.lossyScale + " sprite=" + AssetDatabase.GetAssetPath(sprite) + " bounds=" + renderer.bounds + " shader=" + (renderer.sharedMaterial ? renderer.sharedMaterial.shader.name : "null") + " verts=" + (sprite ? sprite.vertices.Length : 0));
        }
        File.WriteAllText("../BallSort-Verification/scene-sprites.txt",report.ToString());
    }
}
#endif
