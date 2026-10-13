using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RuntimeProbe : MonoBehaviour {
 static bool installed;
 static readonly BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 float started, heartbeat; bool opened, launched, parsed, finished, captured, audioChecked; string lastScene="";
 object player; Assembly game;
 public static void Initialize() {
  if(installed) return; installed=true;
  var go=new GameObject("ChartImportRuntimeProbe"); DontDestroyOnLoad(go); go.AddComponent<RuntimeProbe>();
 }
 static void Log(string text){Debug.Log("[CHART-IMPORT-TEST] "+text);}
 object Field(object obj,string name){return obj.GetType().GetField(name,All).GetValue(obj);}
 void Set(object obj,string name,object value){obj.GetType().GetField(name,All).SetValue(obj,value);}
 object Prop(object obj,string name){return obj.GetType().GetProperty(name,All).GetValue(obj,null);}
 void StaticSet(string name,object value){game.GetType("UserMemory").GetProperty(name,All).SetValue(null,value,null);}
 void Awake(){Application.runInBackground=true;started=Time.realtimeSinceStartup;game=Assembly.Load("Assembly-CSharp");Log("Probe started; saves and score uploads disabled in test copy");}
 void Update(){
  try {
   string scene=SceneManager.GetActiveScene().name;
   if(scene!=lastScene){Log("Scene "+scene);lastScene=scene;}
   if(parsed && scene.Contains("Settlement")){Log("PASS playback reached native results scene");Application.Quit();return;}
   float elapsed=Time.realtimeSinceStartup-started;
   if(elapsed-heartbeat>10){heartbeat=elapsed;Log("Heartbeat elapsed="+elapsed+" scene="+scene);}
   if(elapsed>120){Log("FAIL timeout");Application.Quit();return;}
   if(!opened && elapsed>8){
    var opener=UnityEngine.Object.FindObjectOfType(game.GetType("OpeningSpectrumScript"));
    if(opener!=null){opened=true;opener.GetType().GetMethod("PlayGame",All).Invoke(opener,null);Log("Opening -> song selection");}
   }
   if(!launched && elapsed>12 && scene.Contains("SelectSong")){
    StaticSet("ButtonNumber",4);StaticSet("IsHard",0);StaticSet("DisplayAdjustment",0);StaticSet("JudgeCompensate",0);
    var packages=Resources.FindObjectsOfTypeAll(game.GetType("SongInfoPackageScript"));
    object target=null;
    foreach(var controller in Resources.FindObjectsOfTypeAll(game.GetType("SelectSongController"))){
     var cores=Field(controller,"songInfoCoreEasyList4T") as IEnumerable;
     if(cores!=null)foreach(var core in cores)if((string)Field(core,"bmsCoreName")=="lazhuren"){target=core;break;}
    }
    foreach(var package in packages){
     var info=Field(package,"songInfoCore");if(info==null)continue;
     if((string)Field(info,"bmsCoreName")!="lazhuren" || (string)Field(info,"hardMode")!="easy"){
      if(target==null || !((Component)package).gameObject.activeInHierarchy)continue;
      package.GetType().GetMethod("Reset",All).Invoke(package,new[]{target});info=target;
     }
     Log("Selected "+Prop(info,"RealName")+" duration="+Field(info,"songLength"));
     ((Component)package).gameObject.SetActive(true);
     StaticSet("SceneName","SelectSongScene");
     package.GetType().GetMethod("CleanToPlay",All).Invoke(package,null);
     launched=true;SceneManager.LoadScene("UIB_PlayingScene");break;
    }
   }
   if(launched && !parsed && scene=="UIB_PlayingScene"){
    player=UnityEngine.Object.FindObjectOfType(game.GetType("UIB_SongPlayer"));
    if(player!=null && (bool)Prop(player,"FinishPrepare")){
     int taps=0,holds=0;var lanes=(IEnumerable)Field(player,"noteByTrack");
     var entityType=game.GetTypes().First(t=>t.Name=="NoteEntity");
     foreach(IEnumerable lane in lanes)foreach(var sheath in lane){
      var entityProp=sheath.GetType().GetProperties(All).FirstOrDefault(p=>entityType.IsAssignableFrom(p.PropertyType));
      object entity=entityProp!=null?entityProp.GetValue(sheath,null):null;
      if(entity==null){var f=sheath.GetType().GetFields(All).FirstOrDefault(x=>entityType.IsAssignableFrom(x.FieldType));if(f!=null)entity=f.GetValue(sheath);}
      if(entity==null)continue;
      string kind=entity.GetType().Name;
      if(kind=="Note"||kind=="LongNote"){
       if(kind=="Note")taps++;else holds++;
       Log(kind+" track="+Prop(entity,"Track")+" ticks="+Prop(entity,"InsertTime")+(kind=="LongNote"?" end="+Prop(entity,"EndTime"):""));
      }
     }
     long length=Convert.ToInt64(Field(player,"bgmLength"));
     Log("PARSED taps="+taps+" holds="+holds+" bgmTicks="+length+" cue="+Field(player,"bgmId"));
     if(taps!=24||holds!=4||length!=240000000)throw new Exception("Unexpected chart or audio duration");
     parsed=true;Log("PASS chart and CRI audio loaded");
    }
   }
   if(parsed && !finished && player!=null && scene=="UIB_PlayingScene"){
    long ticks=Convert.ToInt64(Prop(player,"PlayTicks"));
    if(ticks>65000000 && !captured){
     captured=true;ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/musynx-test/runtime.png")));Log("Captured game render");
     var audio=Field(player,"bgmAudioSourceCri");string status=Prop(audio,"status").ToString();long at=Convert.ToInt64(Prop(audio,"time"));
     long expected=(ticks-Convert.ToInt64(Field(player,"bgmStartTime")))/10000;
     Log("AUDIO status="+status+" timeMs="+at+" expectedMs="+expected);
     if(status!="Playing" || Math.Abs(at-expected)>200)throw new Exception("Native audio is not synchronized");
     Log("PASS native CRI audio playing and synchronized");
    }
    if(ticks>265000000 && !audioChecked){audioChecked=true;var audio=Field(player,"bgmAudioSourceCri");Log("AUDIO end status="+Prop(audio,"status")+" timeMs="+Prop(audio,"time"));}
    if(ticks>350000000){finished=true;throw new Exception("Game did not reach results on time");}
   }
  }catch(Exception e){Log("FAIL "+e);Application.Quit();}
 }
}
