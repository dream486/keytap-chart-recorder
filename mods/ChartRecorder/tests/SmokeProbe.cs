using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChartRecorder;

public sealed class ModSmokeProbe : MonoBehaviour {
 static bool created;const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 float began,phaseAt,pauseAt;int phase;bool opened,paused,resumed;double pausedAudio;float audioPeak;Assembly game;
 IDictionary config;GameChartMod mod;SavedSong a,b;SavedChart aFull,aAlt;string run,capture;bool captureDone;
 static void Log(string s){Debug.Log("[MOD-SMOKE] "+s);}
 public static void Initialize(){if(created)return;created=true;var go=new GameObject("ModSmokeProbe");DontDestroyOnLoad(go);go.AddComponent<ModSmokeProbe>();}
 void Awake(){Application.runInBackground=true;began=Time.realtimeSinceStartup;game=Assembly.Load("Assembly-CSharp");var path=Path.Combine(Path.GetDirectoryName(Application.dataPath),"CustomCharts/smoke.json");config=MiniJSON.jsonDecode(File.ReadAllText(path)) as IDictionary;run=(string)config["run"];capture=config["capture"] as string;Log("Started run="+run);}
 string Value(string k){return (string)config[k];}
 void Next(int p){phase=p;phaseAt=Time.realtimeSinceStartup;Log("Phase="+p);}
 void Assert(bool value,string text){if(!value)throw new Exception(text);Log("PASS "+text);}
 void Call(object obj,string method){obj.GetType().GetMethod(method,All).Invoke(obj,null);}
 void Memory(string prop,object value){game.GetType("UserMemory").GetProperty(prop,All).SetValue(null,value,null);}
 void Update(){try{
  mod=GameChartMod.Instance;if(mod==null)return;string scene=SceneManager.GetActiveScene().name;float now=Time.realtimeSinceStartup;
  if(mod.BackgroundStarted&&mod.Audio.isPlaying){var mix=new float[1024];AudioListener.GetOutputData(mix,0);foreach(float value in mix)audioPeak=Mathf.Max(audioPeak,Mathf.Abs(value));}
  if(now-began>160)throw new Exception("Scenario timed out at phase "+phase+" scene "+scene);
  if(!opened&&now-began>8){var opener=UnityEngine.Object.FindObjectOfType(game.GetType("OpeningSpectrumScript"));if(opener!=null){opened=true;Call(opener,"PlayGame");}}
  if(phase==0&&scene.Contains("SelectSong")&&now-began>12){
   if(run=="import"){
    Assert(mod.Library.Songs.Count==0,"fresh library is empty");mod.OpenPanel();mod.BeginFileImport("song",Value("audioA"),Value("chartA"));Next(1);
   }else{
    Assert(mod.Library.Songs.Count==2,"two songs survive game restart");a=mod.Library.Songs[0];b=mod.Library.Songs[1];aFull=a.Charts[0];aAlt=a.Charts[1];
    Assert(mod.Song.Id==b.Id&&mod.Map.Id==b.Charts[0].Id,"last song and chart restored on startup");
    Assert(a.Charts.Count==2,"multiple charts survive restart");mod.Select(a,aAlt,true);mod.OpenPanel();Next(10);
   }
  }
  if(phase==1&&!mod.IsBusy){Assert(mod.Library.Songs.Count==1&&mod.Song!=null&&mod.Map!=null,"music + JSON import completed inside game");a=mod.Song;aFull=mod.Map;mod.BeginFileImport("chart",null,Value("chartAlt"));Next(2);}
  if(phase==2&&!mod.IsBusy){Assert(a.Charts.Count==2&&mod.Map!=aFull,"add and switch to another chart for same music");aAlt=mod.Map;mod.BeginFileImport("song",Value("audioB"),Value("chartB"));Next(3);}
  if(phase==3&&!mod.IsBusy){Assert(mod.Library.Songs.Count==2&&mod.Song!=a,"second song imported and selected");b=mod.Song;mod.Select(a,aFull,true);Assert(mod.Chart.Notes.Count==28&&mod.Audio.clip.samples==1058400,"switch song changes both chart and audio clip");mod.OpenPanel();Next(4);}
  if(phase==4&&now-phaseAt>2){Prepare();mod.StartSelected();Next(5);}
  if(phase==5&&mod.Player!=null&&Convert.ToBoolean(GameChartMod.Property(mod.Player,"FinishPrepare"))){CheckNotes(28,4);Next(6);}
  if(phase==6&&mod.BackgroundStarted){
   long ticks=Convert.ToInt64(GameChartMod.Property(mod.Player,"PlayTicks"));
   if(!paused&&ticks>30000000){Call(mod.Player,"PlayPause");Assert(Convert.ToInt32(GameChartMod.Property(mod.Player,"playStatus"))==2,"native pause menu opened");paused=true;pausedAudio=mod.AudioMilliseconds;pauseAt=now;}
   if(paused&&!resumed&&now-pauseAt>.4f){Assert(Math.Abs(mod.AudioMilliseconds-pausedAudio)<25,"custom music stays frozen during pause");Call(mod.Player,"PlayResume");resumed=true;}
   if(resumed&&Convert.ToInt32(GameChartMod.Property(mod.Player,"playStatus"))==1&&mod.AudioMilliseconds>pausedAudio+400){Assert(mod.Audio.isPlaying,"music resumes after native countdown");Assert(Math.Abs((ticks-20000000)/10000.0-mod.AudioMilliseconds)<150,"native note clock follows custom audio sample clock");Next(7);}
  }
  if(phase==7&&scene.Contains("Settlement")){Assert(true,"first custom song reaches native results");Back();Next(8);}
  if(phase==8&&scene.Contains("SelectSong")){mod.Select(b,b.Charts[0],true);Assert(mod.Chart.Notes.Count==9&&mod.Audio.clip.samples==529200,"switch to second song changes duration and notes");mod.StartSelected();Next(9);}
  if(phase==9&&mod.Player!=null&&Convert.ToBoolean(GameChartMod.Property(mod.Player,"FinishPrepare"))){CheckNotes(9,1);Next(11);}
  if(phase==11&&scene.Contains("Settlement")){Assert(true,"second custom song reaches native results");Finish();}
  if(phase==10){
   if(!captureDone&&capture!=null){captureDone=true;StartCoroutine(Capture());}
   if(run=="ui"&&now-phaseAt>3){Assert(capture!=null&&File.Exists(capture),"game-native library UI rendered");Finish();return;}
   if(run=="ui")return;
   if(now-phaseAt>2){Prepare();mod.StartSelected();Next(12);}
  }
  if(phase==12&&mod.Player!=null&&Convert.ToBoolean(GameChartMod.Property(mod.Player,"FinishPrepare"))){CheckNotes(10,1);Next(13);}
  if(phase==13&&scene.Contains("Settlement")){Assert(true,"alternate chart plays after restart");Assert(audioPeak>.0001f,"custom music reaches Unity audio output mixer peak="+audioPeak);Back();Next(14);}
  if(phase==14&&scene.Contains("SelectSong")){mod.UseOriginal();Assert(mod.Song==null,"original-song fallback selectable inside game");
   var asset=Resources.FindObjectsOfTypeAll<TextAsset>().FirstOrDefault(t=>t.name=="lazhuren4T_easy");if(asset!=null)Assert(GameChartMod.GetChart(asset)==asset.text,"original chart restored when library selection cleared");mod.Select(b,b.Charts[0],true);Finish();}
 }catch(Exception e){Log("FAIL "+e);Write("FAIL",e.ToString());Application.Quit();}}
 void Prepare(){Memory("DisplayAdjustment",0);Memory("JudgeCompensate",0);}
 void CheckNotes(int expectedCount,int expectedHolds){
  var entityType=game.GetTypes().First(t=>t.Name=="NoteEntity");var actual=new List<string>();int holds=0;
  foreach(IEnumerable lane in (IEnumerable)GameChartMod.Field(mod.Player,"noteByTrack"))foreach(var sheath in lane){
   var prop=sheath.GetType().GetProperties(All).FirstOrDefault(p=>entityType.IsAssignableFrom(p.PropertyType));object entity=prop==null?null:prop.GetValue(sheath,null);
   if(entity==null){var f=sheath.GetType().GetFields(All).FirstOrDefault(x=>entityType.IsAssignableFrom(x.FieldType));if(f!=null)entity=f.GetValue(sheath);}
   if(entity==null)continue;string kind=entity.GetType().Name;if(kind!="Note"&&kind!="LongNote")continue;
   if(kind=="LongNote")holds++;actual.Add(GameChartMod.Property(entity,"Track")+":"+GameChartMod.Property(entity,"InsertTime")+":"+(kind=="LongNote"?GameChartMod.Property(entity,"EndTime").ToString():""));
  }
  var expected=mod.Chart.Notes.Select(n=>(new[]{3,4,5,6})[n.Lane]+":"+((long)Math.Round((n.Time+2000)*10000)-10000)+":"+(n.End.HasValue?((long)Math.Round((n.End.Value+2000)*10000)-10000).ToString():"")).OrderBy(s=>s).ToArray();
  Assert(actual.Count==expectedCount&&holds==expectedHolds&&actual.OrderBy(s=>s).SequenceEqual(expected),"native note positions match selected chart: "+expectedCount+" notes / "+holds+" holds");
  Assert(Convert.ToInt64(GameChartMod.Field(mod.Player,"bgmLength"))==(long)Math.Round(mod.Song.DurationMs*10000),"native duration matches selected music");
  Log("Audio listeners="+UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(l=>l.enabled));
 }
 void Back(){var t=game.GetType("NewSettlementController");var obj=UnityEngine.Object.FindObjectOfType(t);if(obj==null){t=game.GetType("SettlementController");obj=UnityEngine.Object.FindObjectOfType(t);}if(obj==null)throw new Exception("Results controller missing");Call(obj,"BackToSelectSong");}
 IEnumerator Capture(){yield return new WaitForEndOfFrame();var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();Directory.CreateDirectory(Path.GetDirectoryName(capture));File.WriteAllBytes(capture,texture.EncodeToPNG());Destroy(texture);Log("UI captured "+capture);}
 void Finish(){Log("PASS scenario "+run);Write("PASS","scenario complete");Application.Quit();}
 void Write(string status,string detail){string file=Path.Combine(Path.GetDirectoryName(Application.dataPath),"CustomCharts/smoke-"+run+".json");File.WriteAllText(file,MiniJSON.jsonEncode(new Hashtable{{"status",status},{"detail",detail},{"songs",mod==null?0:mod.Library.Songs.Count}}));}
}
