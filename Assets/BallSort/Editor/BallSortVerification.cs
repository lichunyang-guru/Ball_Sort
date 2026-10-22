using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// Explicit menu-driven verification only; never runs automatically on project load.
[InitializeOnLoad]
public static class BallSortVerification
{
    static readonly string Output=Path.GetFullPath("../BallSort-Verification");
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static int stage;static double next;
    static BallSortVerification(){EditorApplication.update+=Tick;}
    [MenuItem("Tools/Ball Sort/Enable Native Input Verification")]
    static void EnableNativeInput()
    {
        if(!EditorApplication.isPlaying)return;
        Application.runInBackground=true;
        Directory.CreateDirectory(Output);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
    }
    [MenuItem("Tools/Ball Sort/Capture Native Input State")]
    static void CaptureNativeInput()
    {
        if(!EditorApplication.isPlaying)return;
        Directory.CreateDirectory(Output);
        Capture("native-input-"+DateTime.Now.ToString("HHmmss"));
        EditorApplication.QueuePlayerLoopUpdate();
    }
    [MenuItem("Tools/Ball Sort/Verify Portrait And Landscape")]
    static void Start(){Directory.CreateDirectory(Output);Size(540,960);SessionState.SetBool("BallSortVerify",true);stage=0;next=0;EditorApplication.isPlaying=true;}
    static void Size(int w,int h)
    {
        var asm=typeof(Editor).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
        var sizeType=asm.GetType("UnityEditor.GameViewSize");var enumType=asm.GetType("UnityEditor.GameViewSizeType");
        var ctor=sizeType.GetConstructor(Flags,null,new[]{enumType,typeof(int),typeof(int),typeof(string)},null);
        var size=ctor.Invoke(new object[]{Enum.ToObject(enumType,1),w,h,"BallSort verification "+w+"x"+h});
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int total=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,total-1);view.Show();view.Focus();
    }
    static void Tick()
    {
        if(!SessionState.GetBool("BallSortVerify",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        EditorApplication.QueuePlayerLoopUpdate();
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(Time.time<next)return;
        try
        {
            switch(stage++)
            {
                case 0:Application.runInBackground=true;GameLanguageManager.Instance.CurrentLanguage=SystemLanguage.English;next=Time.time+3;break;
                case 1:Capture("unity-portrait");next=Time.time+1;break;
                case 2:Size(960,540);next=Time.time+3;break;
                case 3:Capture("unity-landscape");next=Time.time+1;break;
                case 4:Click("Tube (8)");next=Time.time+1;break;
                case 5:Capture("unity-selected");Click("LongTube");next=Time.time+2;break;
                case 6:Capture("unity-after-move");PrepareCompletionFixture();next=Time.time+1;break;
                case 7:Click("Tube (2)");next=Time.time+1;break;
                case 8:Click("Tube (1)");next=Time.time+2;break;
                case 9:Capture("unity-completion-fixture");SessionState.SetBool("BallSortVerify",false);Debug.Log("BallSort verification captures saved: "+Output);break;
            }
        }
        catch(Exception e){SessionState.SetBool("BallSortVerify",false);File.WriteAllText(Output+"/error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Click(string name)
    {
        var game=UnityEngine.Object.FindObjectOfType<GameController>();var tube=UnityEngine.Object.FindObjectsOfType<TubeView>().First(t=>t.name==name);
        var guide=UnityEngine.Object.FindObjectOfType<GuidePanel>();
        if(guide!=null)typeof(GuidePanel).GetMethod("HandlePointerDown",Flags).Invoke(guide,new object[]{(Vector2)Camera.main.WorldToScreenPoint(tube.transform.position)});
        typeof(GameController).GetMethod("HandleTubeClicked",Flags).Invoke(game,new object[]{tube});
    }
    static void PrepareCompletionFixture()
    {
        var game=UnityEngine.Object.FindObjectOfType<GameController>();
        var entries=(System.Collections.Generic.List<GameController.TubeLevelEntry>)typeof(GameController).GetField("levelEntries",Flags).GetValue(game);
        foreach(var entry in entries)entry.balls.Clear();
        entries[0].balls.AddRange(new[]{BallColorType.Green,BallColorType.Green,BallColorType.Green});
        entries[1].balls.Add(BallColorType.Green);game.RestartLevel();
    }
    static void Capture(string name)
    {
        string png=Output+"/"+name+".png";if(File.Exists(png))File.Delete(png);
        ScreenCapture.CaptureScreenshot(png);
        var game=UnityEngine.Object.FindObjectOfType<GameController>();var states=new JArray();
        var models=(IDictionary)typeof(GameController).GetField("tubeModels",Flags).GetValue(game);
        foreach(DictionaryEntry e in models)
        {
            var tube=(TubeView)e.Key;var model=(TubeModel)e.Value;
            states.Add(new JObject{{"name",tube.name},{"capacity",model.Capacity},{"balls",new JArray(model.Balls.Select(c=>(int)c))},{"completed",model.IsCompleted},{"position",JObject.FromObject(new{x=tube.transform.position.x,y=tube.transform.position.y,z=tube.transform.position.z})}});
        }
        var output=new JObject{{"width",Camera.main.pixelWidth},{"height",Camera.main.pixelHeight},{"cameraSize",Camera.main.orthographicSize},{"cameraAspect",Camera.main.aspect},{"tubes",states},{"ballViews",UnityEngine.Object.FindObjectsOfType<BallView>().Length}};
        var guide=UnityEngine.Object.FindObjectOfType<GuidePanel>();
        if(guide!=null){output["guideStep"]=(int)typeof(GuidePanel).GetField("currentStep",Flags).GetValue(guide);output["guideShowing"]=(bool)typeof(GuidePanel).GetField("isGuideShow",Flags).GetValue(guide);}
        output["completionNotifications"]=(int)typeof(GameConfig).GetField("curCompletedTubeCount",Flags).GetValue(GameConfig.Instance);
        File.WriteAllText(Output+"/"+name+".json",output.ToString());
    }
}
