using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Rebuilds the supplied playable's decoded hierarchy using native Unity objects.
public static class BallSortReconstruction
{
    const string Root = "Assets/BallSort/";
    static Dictionary<int,Object> refs;
    static readonly List<string> notes = new List<string>();
    [MenuItem("Tools/Ball Sort/Rebuild Extracted Playable")]
    public static void QueueBuild() { EditorApplication.delayCall += Build; }
    [MenuItem("Tools/Ball Sort/Rebuild Scene From Imported Assets")]
    public static void QueueScene() { EditorApplication.delayCall += ()=>BuildScene(false); }
    static IEnumerable<JObject> Flatten(IEnumerable<JToken> roots)
    {
        foreach (JObject o in roots) { yield return o; foreach(var c in Flatten(o["children"] ?? new JArray())) yield return c; }
    }
    public static void Build() { BuildScene(true); }
    static void BuildScene(bool importAssets)
    {
        try
        {
            notes.Clear();
            var data=JObject.Parse(File.ReadAllText(Root+"Editor/SourceScene.json"));
            if(!importAssets)BallSortSourceAssets.RepairExistingGeometry(data);
            refs=importAssets ? BallSortSourceAssets.Build(data) : BallSortSourceAssets.LoadExisting(data);
            refs[780]=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ball=(JObject)data["prefabs"].First(p=>(string)p["name"]=="Ball");
            var prefabNodes=Flatten(new[]{ball}).ToList();
            CreateNodes(prefabNodes); ApplyNodes(prefabNodes);
            foreach(var node in prefabNodes)((GameObject)refs[(int)node["id"]]).SetActive((bool?)node["enabled"]??true);
            var ballRoot=(GameObject)refs[(int)ball["id"]];ballRoot.SetActive(true);
            Directory.CreateDirectory(Root+"Generated");
            var saved=PrefabUtility.SaveAsPrefabAsset(ballRoot,Root+"Generated/Ball.prefab");
            foreach(var node in prefabNodes)
            {
                string path=(string)node["path"];string relative=path=="Ball"?"":path.Substring("Ball/".Length);
                var target=relative==""?saved:saved.transform.Find(relative).gameObject;
                refs[(int)node["id"]]=target;
                foreach(var c in node["components"]){var type=FindType((string)c["type"]);if(type!=null)refs[(int)c["id"]]=target.GetComponent(type);}
            }
            Object.DestroyImmediate(ballRoot);
            var nodes=Flatten(data["scenes"][0]["objects"]).ToList();
            CreateNodes(nodes);ApplyNodes(nodes);
            var camera=(Camera)refs[82374]; camera.tag="MainCamera";camera.transform.localScale=Vector3.one;camera.ResetAspect();
            var config=(GameConfig)refs[82130];config.targetStepCount=50;config.cycleStepCount=1;config.targetCompletedTubeCount=4;
            // Built-in sprite materials do not require illumination; retain a conventional scene light.
            var lightObject=new GameObject("Directional Light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.enabled=false;
            foreach(var node in nodes) ((GameObject)refs[(int)node["id"]]).SetActive((bool?)node["enabled"]??true);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Home.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Home.unity",true)};
            AssetDatabase.SaveAssets();
            File.WriteAllText(Root+"Editor/ImportReport.txt","SUCCESS\n"+string.Join("\n",notes));
            Debug.Log("Ball Sort reconstruction: scene and prefab saved. "+notes.Count+" import notes.");
        }
        catch(Exception e){File.WriteAllText(Root+"Editor/ImportReport.txt",e.ToString());Debug.LogException(e);}
    }
    static Type FindType(string name)
    {
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){var t=a.GetType(name);if(t!=null)return t;}return null;
    }
    static void CreateNodes(List<JObject> nodes)
    {
        foreach(var n in nodes)
        {
            bool rect=n["components"].Any(c=>(string)c["type"]=="UnityEngine.RectTransform");
            var go=rect?new GameObject((string)n["name"],typeof(RectTransform)):new GameObject((string)n["name"]);
            go.SetActive(false);go.layer=(int?)n["layer"]??0;refs[(int)n["id"]]=go;
        }
        foreach(var n in nodes)
        {
            var go=(GameObject)refs[(int)n["id"]];
            if(n["parentId"]?.Type==JTokenType.Integer && refs.TryGetValue((int)n["parentId"],out var parent))go.transform.SetParent(((GameObject)parent).transform,false);
            foreach(var c in n["components"])
            {
                var type=FindType((string)c["type"]);
                if(type==null){notes.Add("Missing type "+c["type"]);continue;}
                if(!typeof(Component).IsAssignableFrom(type))continue;
                var component=go.GetComponent(type);if(component==null)component=go.AddComponent(type);refs[(int)c["id"]]=component;
            }
        }
    }
    static void ApplyNodes(List<JObject> nodes)
    {
        foreach(var n in nodes)foreach(var c in n["components"])
        {
            if(!refs.TryGetValue((int)c["id"],out var target)||target==null)continue;
            var fields=(JObject)c["fields"];
            if(target is Transform tr)
            {
                if(tr is RectTransform rt){rt.pivot=V2(fields["pivot"]);rt.anchorMin=V2(fields["anchorMin"]);rt.anchorMax=V2(fields["anchorMax"]);rt.sizeDelta=V2(fields["sizeDelta"]);rt.anchoredPosition3D=V3(fields["anchoredPosition3D"]);}
                else tr.localPosition=V3(fields["position"]);
                tr.localRotation=(Quaternion)ConvertValue(fields["rotation"],typeof(Quaternion));tr.localScale=V3(fields["scale"]);continue;
            }
            var so=new SerializedObject(target);
            foreach(var f in fields.Properties())
            {
                if(target is Camera && (f.Name=="aspect"||f.Name=="cameraType"))continue;
                // Sorting IDs belong to the exported project. All gameplay sprites share this layer.
                if(target is Renderer && f.Name=="sortingLayerID"){((Renderer)target).sortingLayerID=0;continue;}
                try
                {
                    if(target is Animator animator && f.Name=="animatorController"){so.ApplyModifiedPropertiesWithoutUndo();animator.runtimeAnimatorController=(RuntimeAnimatorController)ConvertValue(f.Value,typeof(RuntimeAnimatorController));so.Update();continue;}
                    if(target is Collider2D collider && f.Name=="material"){collider.sharedMaterial=(PhysicsMaterial2D)ConvertValue(f.Value,typeof(PhysicsMaterial2D));continue;}
                    var p=so.FindProperty(f.Name);
                    if(p!=null){Assign(p,f.Value);continue;}
                    var prop=target.GetType().GetProperty(f.Name,BindingFlags.Instance|BindingFlags.Public);
                    if(prop!=null&&prop.CanWrite){so.ApplyModifiedPropertiesWithoutUndo();prop.SetValue(target,ConvertValue(f.Value,prop.PropertyType));so.Update();continue;}
                    var field=target.GetType().GetField(f.Name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if(field!=null){so.ApplyModifiedPropertiesWithoutUndo();field.SetValue(target,ConvertValue(f.Value,field.FieldType));so.Update();continue;}
                    if(!new[]{"lightmapSceneIndex","commandBufferCount","renderOrder","hasTransformHierarchy","humanBones","isFallbackOverlay"}.Contains(f.Name))notes.Add(target.GetType().Name+" omitted "+f.Name);
                }
                catch(Exception e){notes.Add(target.name+" "+f.Name+": "+e.GetBaseException().Message);}
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            if(target is Behaviour behavior && c["enabled"]!=null)behavior.enabled=(int)c["enabled"]!=0;
        }
    }
    static Vector2 V2(JToken v)=>v==null?Vector2.zero:new Vector2((float)v["x"],(float)v["y"]);
    static Vector3 V3(JToken v)=>v==null?Vector3.zero:new Vector3((float)v["x"],(float)v["y"],(float)v["z"]);
    static object ConvertValue(JToken v,Type type)
    {
        if(v==null||v.Type==JTokenType.Null)return null;
        if(typeof(Object).IsAssignableFrom(type)){refs.TryGetValue((int?)v["$ref"]??0,out var o);return o;}
        if(type.IsEnum)return Enum.ToObject(type,(int)v);
        if(type==typeof(Vector2))return V2(v);if(type==typeof(Vector3))return V3(v);
        if(type==typeof(Vector4))return new Vector4((float)v["x"],(float)v["y"],(float)v["z"],(float)v["w"]);
        if(type==typeof(Quaternion))return new Quaternion((float)v["x"],(float)v["y"],(float)v["z"],(float)v["w"]);
        if(type==typeof(Color))return new Color((float)v["r"],(float)v["g"],(float)v["b"],(float)v["a"]);
        if(type==typeof(Rect))return v is JArray?new Rect((float)v[0],(float)v[1],(float)v[2],(float)v[3]):new Rect((float)v["x"],(float)v["y"],(float)v["width"],(float)v["height"]);
        if(type==typeof(LayerMask))return (LayerMask)(int)v;
        if(type.IsArray){var a=Array.CreateInstance(type.GetElementType(),v.Count());int i=0;foreach(var e in v)a.SetValue(ConvertValue(e,type.GetElementType()),i++);return a;}
        return v.ToObject(type);
    }
    static void Assign(SerializedProperty p,JToken v)
    {
        if(p.propertyType==SerializedPropertyType.ObjectReference){p.objectReferenceValue=(Object)ConvertValue(v,typeof(Object));return;}
        if(p.isArray&&p.propertyType!=SerializedPropertyType.String&&v is JArray array){p.arraySize=array.Count;for(int i=0;i<array.Count;i++)Assign(p.GetArrayElementAtIndex(i),array[i]);return;}
        if(v==null||v.Type==JTokenType.Null)return;
        switch(p.propertyType)
        {
            case SerializedPropertyType.Integer:p.intValue=(int)v;break;
            case SerializedPropertyType.Boolean:p.boolValue=v.Type==JTokenType.Boolean?(bool)v:(int)v!=0;break;
            case SerializedPropertyType.Float:p.floatValue=(float)v;break;
            case SerializedPropertyType.String:p.stringValue=(string)v;break;
            case SerializedPropertyType.Enum:p.intValue=(int)v;break;
            case SerializedPropertyType.Color:p.colorValue=(Color)ConvertValue(v,typeof(Color));break;
            case SerializedPropertyType.Vector2:p.vector2Value=V2(v);break;
            case SerializedPropertyType.Vector3:p.vector3Value=V3(v);break;
            case SerializedPropertyType.Vector4:p.vector4Value=(Vector4)ConvertValue(v,typeof(Vector4));break;
            case SerializedPropertyType.Quaternion:p.quaternionValue=(Quaternion)ConvertValue(v,typeof(Quaternion));break;
            case SerializedPropertyType.LayerMask:p.intValue=(int)v;break;
            case SerializedPropertyType.Rect:p.rectValue=(Rect)ConvertValue(v,typeof(Rect));break;
            default:if(v is JObject obj)foreach(var f in obj.Properties()){var child=p.FindPropertyRelative(f.Name);if(child!=null)Assign(child,f.Value);}break;
        }
    }
}
