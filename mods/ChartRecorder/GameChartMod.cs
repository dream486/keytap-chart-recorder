using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChartRecorder {
 public sealed class GameChartMod : MonoBehaviour {
  public const string Version="0.2.0";static GameChartMod instance;
  const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  public ProgressStore Progress {get;private set;} public Library Library {get;private set;} public SavedSong Song {get;private set;} public SavedChart Map {get;private set;}
  public RecordedChart Chart {get;private set;}public AudioSource Audio {get;private set;}
  public bool PanelOpen {get;private set;} public bool IsBusy {get{return request!=null;}}
  public object Player {get;private set;} public bool BackgroundStarted {get;private set;}
  public double AudioMilliseconds {get{return Audio!=null&&Audio.clip!=null?Audio.timeSamples*1000.0/Audio.clip.frequency:0;}}
  string gameRoot,dataRoot,message="导入 JSON 谱面和对应音乐，使用蜡烛人槽位游玩。",lastScene="";
  string converted,requestFile,requestMode;SavedSong requestSong;Process request;
  CriAtomSource hitAudio;string sessionSong,sessionChart;PlayResult sessionResult;bool scoreDetails;Vector2 scoreScroll;bool recorded,progressHealthy=true;public int HitCount {get;private set;} public CriAtomSource HitAudio {get{return hitAudio;}}
  bool libraryHealthy=true,audioPaused,audioEnded,preview;float volume=.65f;Vector2 songScroll,mapScroll;
  readonly List<WeakReference> tracked=new List<WeakReference>();readonly Dictionary<object,object[]> originals=new Dictionary<object,object[]>();
  GUIStyle titleStyle,textStyle,buttonStyle,smallStyle,selectedStyle;Font font;Texture2D panelTexture,buttonTexture,hoverTexture;
  public static GameChartMod Instance {get{return instance;}}
  public static void Initialize(){if(instance!=null)return;var go=new GameObject("KeyboardChartLibrary");DontDestroyOnLoad(go);go.AddComponent<GameChartMod>();}
  static void Log(string value){UnityEngine.Debug.Log("[CHART-MOD] "+value);}
  public static object Field(object obj,string name){var f=obj.GetType().GetField(name,All);return f==null?Property(obj,name):f.GetValue(obj);}
  static void SetField(object obj,string name,object value){var f=obj.GetType().GetField(name,All);if(f!=null)f.SetValue(obj,ConvertValue(value,f.FieldType));}
  public static object Property(object obj,string name){var p=obj.GetType().GetProperty(name,All);return p==null?null:p.GetValue(obj,null);}
  static void SetProperty(object obj,string name,object value){var p=obj.GetType().GetProperty(name,All);if(p!=null)p.SetValue(obj,ConvertValue(value,p.PropertyType),null);}
  static object ConvertValue(object value,Type type){return value==null||type.IsInstanceOfType(value)?value:Convert.ChangeType(value,type,System.Globalization.CultureInfo.InvariantCulture);}
  static object Decode(string json){return MiniJSON.jsonDecode(json);}
  static string Encode(object value){return MiniJSON.jsonEncode(value);}
  void Awake(){
   instance=this;gameRoot=Path.GetDirectoryName(Application.dataPath);dataRoot=Path.Combine(gameRoot,"CustomCharts");Directory.CreateDirectory(dataRoot);
   Audio=gameObject.AddComponent<AudioSource>();Audio.playOnAwake=false;Audio.loop=false;Audio.spatialBlend=0;Audio.volume=volume;
   Progress=new ProgressStore(Path.Combine(dataRoot,"scores.json"),Decode,Encode);try{Progress.Load();}catch(Exception e){progressHealthy=false;Log("Scores preserved, writes disabled: "+e.Message);message="成绩索引读取失败，保留文件："+e.Message;}
   Library=new Library(Path.Combine(dataRoot,"Library"),Decode,Encode);
   try{Library.Load();}catch(Exception e){libraryHealthy=false;message="曲库索引读取失败，保留现有文件："+e.Message;Log(message);}
   if(libraryHealthy)try{var song=Library.Songs.FirstOrDefault(s=>s.Id==Library.SelectedSong);if(song!=null){var chart=song.Charts.FirstOrDefault(c=>c.Id==Library.SelectedChart);if(chart!=null)Select(song,chart,false);}}
   catch(Exception e){message="上次选择暂时无法载入："+e.Message;Log(message);}
   Log("Initialized v"+Version+" library="+Library.Songs.Count+" root="+dataRoot);
  }
  void Update(){
   string scene=SceneManager.GetActiveScene().name;
   if(scene!=lastScene){lastScene=scene;if(!IsGameplay(scene)){if(BackgroundStarted){Audio.Stop();BackgroundStarted=false;audioPaused=false;audioEnded=false;}Player=null;}}
   if(Input.GetKeyDown(KeyCode.F8)&&!IsGameplay(scene)){PanelOpen=!PanelOpen;if(!PanelOpen&&preview){Audio.Stop();preview=false;}}
   if(request!=null && request.HasExited)FinishRequest();
  }
  public static bool IsGameplay(string scene){return scene.IndexOf("Playing",StringComparison.OrdinalIgnoreCase)>=0;}
  public static bool BlockMenuInput(){return instance!=null&&(instance.PanelOpen||instance.IsBusy);}
  public void OpenPanel(){if(!IsGameplay(SceneManager.GetActiveScene().name))PanelOpen=true;}
  public SavedSong ImportSong(string wav,string json){var s=Library.AddSong(wav,json,false);try{Select(s,s.Charts[0],true);}catch{Library.Songs.Remove(s);throw;}message="已导入："+s.Title;Log("Imported song="+s.Id+" title="+s.Title);return s;}
  public SavedChart ImportChart(SavedSong song,string json){var c=Library.AddChart(song,json,false);try{Select(song,c,true);}catch{song.Charts.Remove(c);throw;}message="已添加谱面："+c.Name;Log("Imported chart="+c.Id+" song="+song.Id);return c;}
  public void Select(SavedSong song,SavedChart map,bool persist){
   if(IsGameplay(SceneManager.GetActiveScene().name))throw new InvalidOperationException("请回到选歌界面后切换曲目");
   var chart=Library.ReadChart(map.File);chart.CheckAudio(song.DurationMs);string text=chart.ToMusynx(song.DurationMs);
   AudioClip clip=Audio.clip;bool replace=Song==null||Song.Id!=song.Id||clip==null;
   if(replace){var wave=WaveData.Read(Library.Resolve(song.Audio),true);chart.CheckAudio(wave.DurationMs);clip=AudioClip.Create("custom_"+song.Id,wave.Frames,wave.Channels,wave.Rate,false);if(!clip.SetData(wave.Samples,0)){Destroy(clip);throw new InvalidDataException("音乐载入失败");}}
   if(persist){string oldSong=Library.SelectedSong,oldMap=Library.SelectedChart;try{Library.SelectedSong=song.Id;Library.SelectedChart=map.Id;Library.Save();}catch{Library.SelectedSong=oldSong;Library.SelectedChart=oldMap;if(replace)Destroy(clip);throw;}}
   scoreDetails=false;Audio.Stop();preview=false;var oldClip=Audio.clip;Audio.clip=clip;Song=song;Map=map;Chart=chart;converted=text;
   if(replace&&oldClip!=null&&oldClip!=clip)Destroy(oldClip);RefreshSongInfos();try{RefreshMenuLabels();}catch(Exception e){Log("Menu label refresh: "+e.Message);}
   message="当前："+song.Title+" / "+map.Name;Log("Selected song="+song.Id+" chart="+map.Id+" notes="+chart.Notes.Count+" durationMs="+song.DurationMs);
  }
  public void UseOriginal(){if(IsGameplay(SceneManager.GetActiveScene().name))return;Audio.Stop();preview=false;var old=Audio.clip;Audio.clip=null;if(old!=null)Destroy(old);Song=null;Map=null;Chart=null;converted=null;Library.SelectedSong="";Library.SelectedChart="";Library.Save();RefreshSongInfos();RefreshMenuLabels();message="当前蜡烛人槽位使用原曲原谱。";}
  public static void ConfigureSongInfo(object info){if(instance==null||info==null||(string)Field(info,"bmsCoreName")!="lazhuren")return;instance.Configure(info);}
  void Configure(object info){
   if(!originals.ContainsKey(info)){originals[info]=new[]{Property(info,"RealName"),Field(info,"songLength"),Field(info,"mustAuto"),Field(info,"songComposersInfo")};tracked.Add(new WeakReference(info));}
   if(Song!=null){SetProperty(info,"RealName","蜡烛人 / "+Song.Title);SetField(info,"songLength",(int)Math.Ceiling(Song.DurationMs/1000)+3);SetField(info,"mustAuto",true);var original=originals[info][3];if(original!=null){var clone=typeof(object).GetMethod("MemberwiseClone",All).Invoke(original,null);SetProperty(clone,"songNameInfo",Song.Title+" / "+Map.Name);SetProperty(clone,"SongNameLine",1);SetField(info,"songComposersInfo",clone);}}
   else{var old=originals[info];SetProperty(info,"RealName",old[0]);SetField(info,"songLength",old[1]);SetField(info,"mustAuto",old[2]);SetField(info,"songComposersInfo",old[3]);}
  }
  void RefreshSongInfos(){foreach(var reference in tracked.ToArray())if(reference.IsAlive)Configure(reference.Target);}
  void RefreshMenuLabels(){
   var game=Assembly.Load("Assembly-CSharp");foreach(string name in new[]{"SongInfoPackageScript","SmallSongInfoPackageScript"}){
    var type=game.GetType(name);if(type==null)continue;
    foreach(var obj in Resources.FindObjectsOfTypeAll(type)){
    var component=obj as Component;var behaviour=obj as Behaviour;if(component==null||!component.gameObject.activeInHierarchy||component.gameObject.scene!=SceneManager.GetActiveScene()||(behaviour!=null&&!behaviour.enabled))continue;var info=Field(obj,"songInfoCore");if(info==null||(string)Field(info,"bmsCoreName")!="lazhuren")continue;
     type.GetMethod("Reset",All).Invoke(obj,new[]{info});
    }
   }
   var controller=UnityEngine.Object.FindObjectOfType(game.GetType("SelectSongController"));
   if(controller!=null){var text=Field(controller,"SmallSelectSongName") as UnityEngine.UI.Text;var pkg=Field(controller,"smallSongInfoPackage") as Array;
    if(text!=null&&pkg!=null&&pkg.Length>9){var go=pkg.GetValue(9) as GameObject;if(go!=null){var p=go.GetComponent(game.GetType("SmallSongInfoPackageScript"));var info=p==null?null:Field(p,"songInfoCore");if(info!=null&&(string)Field(info,"bmsCoreName")=="lazhuren")text.text=(string)Property(info,"RealName");}}
   }
  }
  public static string GetChart(TextAsset asset){if(asset==null)return null;if(instance!=null&&instance.converted!=null&&asset.name.StartsWith("lazhuren",StringComparison.Ordinal))return instance.converted;return asset.text;}
  public static void PlayerReady(object player){
   if(instance==null)return;var info=Field(player,"songInfoCore");if(info==null||(string)Field(info,"bmsCoreName")!="lazhuren"||instance.Song==null)return;
   instance.sessionSong=instance.Song.Id;instance.sessionChart=instance.Map.Id;instance.recorded=false;instance.sessionResult=null;instance.HitCount=0;instance.PrepareHitAudio();
   instance.Audio.Stop();instance.preview=false;instance.Player=player;instance.BackgroundStarted=false;instance.audioPaused=false;instance.audioEnded=false;
   SetField(player,"songOverTime",(float)(Math.Ceiling(instance.Song.DurationMs/1000)+3)*10000000f);SetField(player,"bgmLength",(long)Math.Round(instance.Song.DurationMs*10000));
   Log("Player ready type="+player.GetType().Name+" custom="+instance.Song.Title+" / "+instance.Map.Name);
  }
  public static bool TryPlayBackground(object player,string cue){
   if(instance==null||!ReferenceEquals(instance.Player,player)||cue!="lazhuren")return false;
   if(!instance.BackgroundStarted){instance.Audio.timeSamples=0;instance.Audio.Play();instance.BackgroundStarted=true;instance.audioPaused=false;instance.audioEnded=false;SetField(player,"bgmStartTime",20000000L);Log("Custom background started");}return true;
  }
  public static CriAtomExPlayback StartCri(CriAtomSource source){if(instance!=null&&instance.Player!=null&&ReferenceEquals(Field(instance.Player,"bgmAudioSourceCri"),source))return default(CriAtomExPlayback);return source.Play();}
  public static void SyncPlayer(object player){
   if(instance==null||!ReferenceEquals(instance.Player,player)||!instance.BackgroundStarted)return;
   int state=Convert.ToInt32(Property(player,"playStatus"));
   if(state!=1){if(!instance.audioPaused&&!instance.audioEnded){instance.Audio.Pause();instance.audioPaused=true;}return;}
   if(instance.audioPaused){instance.Audio.UnPause();instance.audioPaused=false;Log("Custom background resumed");}
   if(!instance.Audio.isPlaying){instance.audioEnded=true;return;}
   long time=20000000L+(long)Math.Round(instance.Audio.timeSamples*10000000.0/instance.Audio.clip.frequency);
   // Original Update adds deltaTime immediately afterwards; align its resulting time.
   SetProperty(player,"PlayTicks",time-(long)(Time.deltaTime*10000000f));
  }
  public static void PausePlayer(object player){if(instance!=null&&ReferenceEquals(instance.Player,player)&&Convert.ToInt32(Property(player,"playStatus"))!=1){instance.Audio.Pause();instance.audioPaused=true;}}
  public static bool IsCustomSlot(object info){return instance!=null&&instance.Song!=null&&info!=null&&(string)Field(info,"bmsCoreName")=="lazhuren";}
  void StopMenuPreview(){var type=Assembly.Load("Assembly-CSharp").GetType("SoundEffectScript");foreach(var obj in Resources.FindObjectsOfTypeAll(type)){var source=Field(obj,"previewAudioCri") as CriAtomSource;if(source!=null)source.Stop();}}
  public void StartSelected(){
   if(Song==null||Chart==null)throw new InvalidOperationException("先导入并选择一张谱面");if(IsBusy)throw new InvalidOperationException("请等待导入完成");
   string scene=SceneManager.GetActiveScene().name;if(!scene.Contains("SelectSong"))throw new InvalidOperationException("请先进入原游戏选歌界面");
   Audio.Stop();preview=false;var game=Assembly.Load("Assembly-CSharp");var type=game.GetType("SelectSongController");var controller=UnityEngine.Object.FindObjectOfType(type);if(controller==null)throw new InvalidOperationException("选歌界面尚未准备好");
   var memory=game.GetType("UserMemory");memory.GetProperty("ButtonNumber",All).SetValue(null,4,null);memory.GetProperty("IsHard",All).SetValue(null,0,null);memory.GetProperty("SortNum",All).SetValue(null,0,null);
   var easy=Field(controller,"songInfoCoreEasyList4T") as IList;if(easy==null)throw new InvalidOperationException("基础曲目尚未载入");int index=-1;for(int i=0;i<easy.Count;i++)if((string)Field(easy[i],"bmsCoreName")=="lazhuren"){index=i;break;}if(index<0)throw new InvalidOperationException("没有找到蜡烛人基础曲目");
   foreach(string level in new[]{"Easy","Hard","In"})SetField(controller,"songInfoCore"+level+"List",Field(controller,"songInfoCore"+level+"List4T"));SetField(controller,"songInfoCoreList",easy);SetField(controller,"songIndex",index);SetField(controller,"difficuylty",Enum.Parse(game.GetType("Difficuylty"),"Easy"));SetField(controller,"isHard",false);
   bool small=scene.Contains("Small");memory.GetProperty("SceneName",All).SetValue(null,small?"SmallSelectSongScene":"SelectSongScene",null);
   if(small){var packages=Field(controller,"smallSongInfoPackage") as Array;for(int i=0;i<packages.Length;i++){int at=((index+i-9)%easy.Count+easy.Count)%easy.Count;var package=packages.GetValue(i);package.GetType().GetMethod("Reset",All).Invoke(package,new[]{easy[at]});}}
   else{var packages=Field(controller,"songInfoPackageList") as IList;int center=packages.Count/2;for(int i=0;i<packages.Count;i++){var package=packages[i];int at=index+i-center;package.GetType().GetMethod("Reset",All).Invoke(package,new[]{easy[(at%easy.Count+easy.Count)%easy.Count]});package.GetType().GetMethod("SetIndex",All).Invoke(package,new[]{(object)at,Enum.Parse(game.GetType("Difficuylty"),"Easy")});}}
   PanelOpen=false;type.GetMethod(small?"SmallSelectCurrentSong":"SelectCurrentSong",All).Invoke(controller,null);StopMenuPreview();message="已进入官方开歌设置，可调整速度与原有皮肤／场景。";Log("Opened official pre-play settings for "+Song.Id+" / "+Map.Id);
  }
  public static int BestSync(object info,int original){if(!IsCustomSlot(info))return original;var e=instance.Progress.Get(instance.Song.Id,instance.Map.Id);return e==null||e.Best==null?0:e.Best.Sync;}
  public static bool UpdateCustomScore(object info,int combo){
   if(!IsCustomSlot(info))return false;var mod=instance;if(mod.sessionResult!=null){SetProperty(info,"MaxComboThis",mod.sessionResult.MaxCombo);SetProperty(info,"SyncNumberThis",mod.sessionResult.Sync);return true;}var grade=Assembly.Load("Assembly-CSharp").GetType("BMSLib.JudgeGrade");int score=Convert.ToInt32(grade.GetMethod("GetSyncNumber",All).Invoke(null,new object[]{combo}));
   SetProperty(info,"MaxComboThis",combo);SetProperty(info,"SyncNumberThis",score);var previous=mod.Progress.Get(mod.sessionSong??mod.Song.Id,mod.sessionChart??mod.Map.Id);bool fresh=previous==null||previous.Best==null||score>previous.Best.Sync;SetProperty(info,"NewRecordThis",fresh);
   if(!mod.recorded&&mod.Player!=null){mod.recorded=true;var r=new PlayResult{Sync=score,MaxCombo=combo,At=DateTime.UtcNow.ToString("o"),Scene=SceneManager.GetActiveScene().name};r.Ex=Count(grade,"TotalEx");r.Exact=Count(grade,"TotalExact");r.Great=Count(grade,"TotalGreat");r.Right=Count(grade,"TotalRight");r.Miss=Count(grade,"TotalMiss");mod.sessionResult=r;
    try{if(!mod.progressHealthy)throw new IOException("成绩文件读取失败，不能覆盖现有文件");mod.Progress.Record(mod.sessionSong,mod.sessionChart,r);Log("Saved native score="+score+" combo="+combo+" song="+mod.sessionSong+" chart="+mod.sessionChart);}catch(Exception e){mod.message="本局成绩已结算，但保存失败："+e.Message;Log(mod.message);}}
   return true;
  }
  static int Count(Type type,string name){return Convert.ToInt32(type.GetProperty(name,All).GetValue(null,null));}
  public void SetHitSound(int choice,float loudness){Progress.HitSound=Math.Max(0,Math.Min(2,choice));Progress.HitVolume=Mathf.Clamp01(loudness);if(progressHealthy)Progress.Save();}
  void PrepareHitAudio(){try{if(hitAudio==null){hitAudio=gameObject.AddComponent<CriAtomSource>();hitAudio.playOnStart=false;hitAudio.use3dPositioning=false;}if(CriAtom.GetCueSheet("ChartRecorderHits")==null)CriAtom.AddCueSheet("ChartRecorderHits","selectSceneSoundEffect.acb",null,null);hitAudio.cueSheet="ChartRecorderHits";}catch(Exception e){Log("Hit audio unavailable: "+e.Message);}}
  public void PreviewHit(){PrepareHitAudio();PlayHit();}
  void PlayHit(){if(Progress.HitSound==0||hitAudio==null)return;hitAudio.volume=Progress.HitVolume;hitAudio.Play(Progress.HitSound==1?"selectSong_switchSound":"selectSong_select");HitCount++;}
  public static bool AcceptedHit(bool accepted,object note){if(accepted&&instance!=null&&instance.Player!=null&&IsGameplay(SceneManager.GetActiveScene().name))instance.PlayHit();return accepted;}
  void BeginRequest(string mode){BeginFileImport(mode,null,null);}
  // File paths are optional for integration tests; the normal UI opens dialogs.
  public void BeginFileImport(string mode,string audioPath,string chartPath){
   if(!libraryHealthy||IsBusy)return;string helper=Path.Combine(gameRoot,"CustomChartMod/ImportHelper.exe");if(!File.Exists(helper))throw new FileNotFoundException("导入助手缺失，请重新安装 Mod");
   string requests=Path.Combine(dataRoot,"Requests");Directory.CreateDirectory(requests);requestFile=Path.Combine(requests,Guid.NewGuid().ToString("N")+".json");requestMode=mode;requestSong=Song;
   string arguments=mode+" \""+requestFile+"\"";
   if(mode=="song"&&audioPath!=null&&chartPath!=null)arguments+=" \""+audioPath+"\" \""+chartPath+"\"";
   if(mode=="chart"&&chartPath!=null)arguments+=" \""+chartPath+"\"";
   request=Process.Start(new ProcessStartInfo(helper,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(helper)});message="在文件选择窗口中选择音乐和谱面；正在导入…";
  }
  void FinishRequest(){
   try{
    if(!File.Exists(requestFile))throw new IOException("导入助手未返回结果");var result=Decode(File.ReadAllText(requestFile)) as IDictionary;if(result==null)throw new IOException("导入结果无效");
    if(result.Contains("cancelled")){message="已取消导入。";return;}
    if(result.Contains("error"))throw new InvalidDataException((string)result["error"]);
    if(requestMode=="song")ImportSong((string)result["audio"],(string)result["chart"]);else{if(requestSong==null)throw new InvalidOperationException("请先选择歌曲");ImportChart(requestSong,(string)result["chart"]);}
   }catch(Exception e){message="导入失败："+e.Message;Log(message);}
   finally{request.Dispose();request=null;foreach(string p in new[]{requestFile,Path.ChangeExtension(requestFile,".wav")})try{if(File.Exists(p))File.Delete(p);}catch{}requestFile=null;}
  }
  void Safe(Action action){try{action();}catch(Exception e){message=e is TargetInvocationException&&e.InnerException!=null?e.InnerException.Message:e.Message;Log("Action failed: "+message);}}
  Texture2D Solid(Color color){var t=new Texture2D(1,1,TextureFormat.RGBA32,false);t.SetPixel(0,0,color);t.Apply();return t;}
  void Styles(){if(titleStyle!=null)return;font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","SimHei","Arial"},18);titleStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=24,fontStyle=FontStyle.Bold};textStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};smallStyle=new GUIStyle(textStyle){fontSize=13};panelTexture=Solid(new Color(.045f,.055f,.075f,1));buttonTexture=Solid(new Color(.12f,.15f,.19f,1));hoverTexture=Solid(new Color(.15f,.27f,.32f,1));buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=16,wordWrap=true,padding=new RectOffset(12,12,8,8)};buttonStyle.normal.background=buttonTexture;buttonStyle.hover.background=hoverTexture;buttonStyle.active.background=hoverTexture;selectedStyle=new GUIStyle(buttonStyle);selectedStyle.normal.background=hoverTexture;selectedStyle.normal.textColor=new Color(.6f,.95f,1f);}
  void OnGUI(){
   Styles();bool playing=IsGameplay(SceneManager.GetActiveScene().name);GUI.depth=-1000;
   if(!PanelOpen){if(!playing&&GUI.Button(new Rect(Screen.width-225,18,207,42),"敲谱曲库  F8",buttonStyle))PanelOpen=true;return;}
   float scale=Mathf.Min(1.2f,Mathf.Min(Screen.width/1020f,Screen.height/700f));var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-980*scale)/2,(Screen.height-650*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
   GUI.DrawTexture(new Rect(0,0,980,650),panelTexture);GUI.Label(new Rect(24,18,800,38),"敲谱曲库 · 蜡烛人槽位",titleStyle);if(GUI.Button(new Rect(874,18,80,34),"关闭",buttonStyle)&&!IsBusy){PanelOpen=false;if(preview){Audio.Stop();preview=false;}}
   GUI.Label(new Rect(24,62,925,42),"导入敲谱器导出的 JSON 和对应 MP3 / WAV。每首歌曲可以添加多张谱面。",textStyle);
   GUI.Label(new Rect(24,110,355,25),"歌曲（"+Library.Songs.Count+"）",textStyle);GUI.Label(new Rect(403,110,550,25),"谱面",textStyle);
   GUI.enabled=libraryHealthy&&!IsBusy&&!playing&&!scoreDetails;
   songScroll=GUI.BeginScrollView(new Rect(24,142,355,298),songScroll,new Rect(0,0,330,Math.Max(298,Library.Songs.Count*52)));
   for(int i=0;i<Library.Songs.Count;i++){var song=Library.Songs[i];if(GUI.Button(new Rect(0,i*52,329,46),song.Title,Song==song?selectedStyle:buttonStyle))Safe(()=>Select(song,song.Charts[0],true));}GUI.EndScrollView();
   var maps=Song==null?new List<SavedChart>():Song.Charts;mapScroll=GUI.BeginScrollView(new Rect(403,142,551,206),mapScroll,new Rect(0,0,527,Math.Max(206,maps.Count*48)));
   for(int i=0;i<maps.Count;i++){var map=maps[i];if(GUI.Button(new Rect(0,i*48,526,42),map.Name,Map==map?selectedStyle:buttonStyle))Safe(()=>Select(Song,map,true));}GUI.EndScrollView();
   if(Chart!=null)GUI.Label(new Rect(403,352,550,52),Chart.Title+"\n"+Chart.Notes.Count+" 音符 / "+Chart.Notes.Count(n=>n.End.HasValue)+" 长条 / "+Chart.Bpm+" BPM / "+(Song.DurationMs/1000).ToString("0.0")+" 秒",textStyle);
   var stats=Song==null||Map==null?null:Progress.Get(Song.Id,Map.Id);if(stats!=null&&stats.Last!=null)GUI.Label(new Rect(403,410,440,36),"最高 "+(stats.Best.Sync/100.0).ToString("0.00")+"% / 上次 "+(stats.Last.Sync/100.0).ToString("0.00")+"% / 最大连击 "+stats.Best.MaxCombo+" / "+stats.Plays+" 局",smallStyle);
   if(stats!=null&&GUI.Button(new Rect(852,402,102,36),"成绩详情",buttonStyle))scoreDetails=true;
   if(scoreDetails){GUI.enabled=true;GUI.DrawTexture(new Rect(16,100,950,480),panelTexture);GUI.Label(new Rect(35,112,765,34),"本曲本谱成绩 · "+(Map==null?"":Map.Name),titleStyle);if(GUI.Button(new Rect(850,112,90,32),"返回",buttonStyle))scoreDetails=false;
    if(stats==null)GUI.Label(new Rect(35,170,800,32),"尚无成绩记录。",textStyle);else{GUI.Label(new Rect(35,157,875,42),"最高同步率 "+(stats.Best.Sync/100.0).ToString("0.00")+"% · 共 "+stats.Plays+" 局 · 最近 20 局",textStyle);scoreScroll=GUI.BeginScrollView(new Rect(35,200,905,340),scoreScroll,new Rect(0,0,875,Math.Max(340,stats.History.Count*68)));for(int i=0;i<stats.History.Count;i++){var r=stats.History[i];DateTime stamp;string when=DateTime.TryParse(r.At,out stamp)?stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):r.At;GUI.Label(new Rect(0,i*68,870,65),when+"  "+(r.Sync/100.0).ToString("0.00")+"%  最大连击 "+r.MaxCombo+"  "+r.Scene+"\nEX "+r.Ex+" / EXACT "+r.Exact+" / GREAT "+r.Great+" / RIGHT "+r.Right+" / MISS "+r.Miss,smallStyle);}GUI.EndScrollView();}GUI.matrix=old;return;}
   if(GUI.Button(new Rect(24,453,355,45),"＋ 导入歌曲与谱面",buttonStyle))Safe(()=>BeginRequest("song"));
   GUI.enabled=libraryHealthy&&!IsBusy&&!playing&&Song!=null;if(GUI.Button(new Rect(403,453,267,45),"＋ 添加本曲的另一张谱面",buttonStyle))Safe(()=>BeginRequest("chart"));
   if(GUI.Button(new Rect(687,453,267,45),preview?"停止试听":"试听当前歌曲",buttonStyle))Safe(()=>{if(preview){Audio.Stop();preview=false;}else{StopMenuPreview();Audio.timeSamples=0;Audio.Play();preview=true;}});
   GUI.enabled=libraryHealthy&&!IsBusy&&!playing;
   if(GUI.Button(new Rect(24,512,355,45),"切回蜡烛人原曲",buttonStyle))Safe(UseOriginal);
   GUI.enabled=libraryHealthy&&!IsBusy&&!playing&&Chart!=null&&SceneManager.GetActiveScene().name.Contains("SelectSong");if(GUI.Button(new Rect(403,512,551,45),"进入官方开歌设置 · 4 键",buttonStyle))Safe(StartSelected);
   GUI.enabled=libraryHealthy&&!IsBusy&&!playing;GUI.Label(new Rect(24,565,100,24),"打击音",smallStyle);int choice=GUI.SelectionGrid(new Rect(115,560,420,32),Progress.HitSound,new[]{"关闭","原版切换音","原版确认音"},3,buttonStyle);float loudness=GUI.HorizontalSlider(new Rect(553,571,160,18),Progress.HitVolume,0,1);if(choice!=Progress.HitSound||Math.Abs(loudness-Progress.HitVolume)>.005f)Safe(()=>SetHitSound(choice,loudness));if(GUI.Button(new Rect(730,560,100,32),"试音",buttonStyle))Safe(PreviewHit);
   GUI.enabled=true;GUI.Label(new Rect(24,603,930,28),message,smallStyle);
   GUI.Label(new Rect(835,568,45,20),"音量",smallStyle);volume=GUI.HorizontalSlider(new Rect(883,571,70,18),volume,0,1);Audio.volume=volume;GUI.matrix=old;
  }
  void OnDestroy(){if(instance==this)instance=null;if(Audio!=null&&Audio.clip!=null)Destroy(Audio.clip);foreach(var t in new[]{panelTexture,buttonTexture,hoverTexture})if(t!=null)Destroy(t);if(font!=null)Destroy(font);}
 }
}
