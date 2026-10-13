using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace ChartRecorder {
 public sealed class ChartNote { public int Lane; public double Time; public double? End; }
 public sealed class RecordedChart {
  public string Title; public double Bpm,Duration; public List<ChartNote> Notes=new List<ChartNote>();
  public static double Number(object value,string name) {
   if(value==null||value is string||value is bool)throw new InvalidDataException(name+" 必须是数字");
   double n=Convert.ToDouble(value,CultureInfo.InvariantCulture);
   if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException(name+" 无效"); return n;
  }
  public static RecordedChart Parse(object raw) {
   var map=raw as IDictionary;if(map==null)throw new InvalidDataException("谱面必须是 JSON 对象");
   if(Number(map["version"],"version")!=1||Number(map["keys"],"keys")!=4)throw new InvalidDataException("需要敲谱器导出的 version:1、keys:4 谱面");
   var c=new RecordedChart();c.Title=map["title"] as string;
   if(string.IsNullOrWhiteSpace(c.Title)||c.Title.Length>100)throw new InvalidDataException("曲名需为 1–100 个字符");c.Title=c.Title.Trim();
   c.Bpm=Number(map["bpm"],"BPM");c.Duration=Number(map["duration"],"duration");
   if(c.Bpm<30||c.Bpm>300||c.Duration<=0||c.Duration>600000)throw new InvalidDataException("BPM 或歌曲时长超出敲谱器支持范围");
   var notes=map["notes"] as IList;if(notes==null||notes.Count<1||notes.Count>20000)throw new InvalidDataException("需要 1–20000 个音符");
   foreach(var rawNote in notes) {
    var n=rawNote as IDictionary;if(n==null)throw new InvalidDataException("音符格式无效");
    double lane=Number(n["lane"],"lane"),time=Number(n["time"],"time");
    if(lane!=Math.Truncate(lane)||lane<0||lane>3||time<0||time>c.Duration)throw new InvalidDataException("音符轨道或时间无效");
    var note=new ChartNote{Lane=(int)lane,Time=time};
    if(n.Contains("endTime")){double end=Number(n["endTime"],"endTime");if(end<=time||end>c.Duration)throw new InvalidDataException("长条终点无效");note.End=end;}
    c.Notes.Add(note);
   }
   c.Notes=c.Notes.OrderBy(n=>n.Time).ThenBy(n=>n.Lane).ToList();
   var occupied=new double[4];var last=new double[4];for(int i=0;i<4;i++){occupied[i]=-1;last[i]=-1;}
   foreach(var n in c.Notes){if(n.Time==last[n.Lane]||n.Time<occupied[n.Lane])throw new InvalidDataException("同轨音符重复或落在长条内部");last[n.Lane]=n.Time;occupied[n.Lane]=n.End??n.Time;}
   return c;
  }
  public void CheckAudio(double milliseconds) {
   if(milliseconds<=0||milliseconds>600000.1)throw new InvalidDataException("歌曲需在 10 分钟以内");
   if(Notes.Any(n=>(n.End??n.Time)>milliseconds+1))throw new InvalidDataException("音符超出音乐长度，请选择对应歌曲");
  }
  public string ToMusynx(double audioDurationMs) {
   CheckAudio(audioDurationMs);const double lead=2000;
   Func<double,long> ticks=ms=>(long)Math.Round((ms+lead)*10000,MidpointRounding.AwayFromZero);
   var lines=new List<string>{"BPM\t"+Bpm.ToString(CultureInfo.InvariantCulture),"Rank\t3","NoteFactoryPrepare\t100\t100\t200\t1000","DefaultWeatherUIB\tnight","WAV\t02\tlazhuren"};
   var events=new List<KeyValuePair<long,string>>();events.Add(new KeyValuePair<long,string>(ticks(0),"MusicNote\t"+ticks(0)+"\t1\t02"));
   double measure=240000/Bpm;long finish=ticks(audioDurationMs+500);
   for(int i=0;i*measure<audioDurationMs+lead+500;i++){
    long start=(long)Math.Round(i*measure*10000),end=Math.Min(finish,(long)Math.Round((i+1)*measure*10000));
    events.Add(new KeyValuePair<long,string>(start,"BeatLine\t"+start+"\t"+i+"\t--\t"+end));
   }
   int[] tracks={3,4,6,7};foreach(var n in Notes){long time=ticks(n.Time);string head=time+"\t"+tracks[n.Lane]+"\t01";events.Add(new KeyValuePair<long,string>(time,n.End.HasValue?"LongNote\t"+head+"\t"+ticks(n.End.Value):"Note\t"+head));}
   lines.AddRange(events.OrderBy(e=>e.Key).Select(e=>e.Value));return string.Join("\n",lines.ToArray())+"\n";
  }
 }
 public sealed class WaveData {
  public int Channels,Rate,Frames;public float[] Samples;
  public double DurationMs { get {return Frames*1000.0/Rate;} }
  public static WaveData Read(string path,bool samples) {
   using(var file=File.OpenRead(path))using(var reader=new BinaryReader(file)){
    if(file.Length<44||file.Length>250000000||Encoding.ASCII.GetString(reader.ReadBytes(4))!="RIFF")throw new InvalidDataException("音乐转换后的 WAV 无效");
    reader.ReadUInt32();if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="WAVE")throw new InvalidDataException("需要 WAV 音乐");
    int format=0,channels=0,rate=0,bits=0,align=0;long dataAt=0;int dataSize=0;
    while(file.Position+8<=file.Length){string tag=Encoding.ASCII.GetString(reader.ReadBytes(4));uint size=reader.ReadUInt32();long end=file.Position+size;if(end>file.Length)throw new InvalidDataException("WAV 数据不完整");
     if(tag=="fmt "){if(size<16)throw new InvalidDataException("WAV 格式无效");format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadInt32();align=reader.ReadUInt16();bits=reader.ReadUInt16();}
     if(tag=="data"){dataAt=file.Position;dataSize=checked((int)size);}file.Position=end+(size%2);
    }
    if(format!=1||bits!=16||channels<1||channels>2||rate<8000||rate>192000||align!=channels*2||dataAt==0||dataSize%align!=0)throw new InvalidDataException("需要转换后的 16 位单声道或双声道 PCM WAV");
    var wave=new WaveData{Channels=channels,Rate=rate,Frames=dataSize/align};if(wave.DurationMs<=0||wave.DurationMs>600000.1)throw new InvalidDataException("音乐时长需为 0–10 分钟");
    if(samples){wave.Samples=new float[wave.Frames*channels];file.Position=dataAt;for(int i=0;i<wave.Samples.Length;i++)wave.Samples[i]=reader.ReadInt16()/32768f;}
    return wave;
   }
  }
 }
 public sealed class SavedChart {public string Id,Name,File;}
 public sealed class SavedSong {public string Id,Title,Audio;public double DurationMs;public List<SavedChart> Charts=new List<SavedChart>();}
 public sealed class Library {
  public readonly string Root;public readonly List<SavedSong> Songs=new List<SavedSong>();public string SelectedSong="",SelectedChart="";
  readonly Func<string,object> decode;readonly Func<object,string> encode;
  public Library(string root,Func<string,object> decodeJson,Func<object,string> encodeJson){Root=Path.GetFullPath(root);decode=decodeJson;encode=encodeJson;Directory.CreateDirectory(Root);}
  public string Resolve(string relative){if(string.IsNullOrEmpty(relative)||Path.IsPathRooted(relative))throw new InvalidDataException("曲库路径无效");string p=Path.GetFullPath(Path.Combine(Root,relative));if(!p.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("曲库路径越界");return p;}
  public RecordedChart ReadChart(string relative){string file=Resolve(relative);if(new FileInfo(file).Length>3000000)throw new InvalidDataException("谱面不能超过 3 MB");return RecordedChart.Parse(decode(System.IO.File.ReadAllText(file)));}
  public void Load(){string file=Path.Combine(Root,"library.json");if(!System.IO.File.Exists(file))return;var data=decode(System.IO.File.ReadAllText(file)) as IDictionary;if(data==null||RecordedChart.Number(data["version"],"version")!=1)throw new InvalidDataException("曲库索引格式无效");
   var list=data["songs"] as IList;if(list==null)throw new InvalidDataException("曲库索引无效");foreach(IDictionary song in list){var s=new SavedSong{Id=(string)song["id"],Title=(string)song["title"],Audio=(string)song["audio"],DurationMs=RecordedChart.Number(song["durationMs"],"durationMs")};Resolve(s.Audio);foreach(IDictionary chart in (IList)song["charts"]){var c=new SavedChart{Id=(string)chart["id"],Name=(string)chart["name"],File=(string)chart["file"]};Resolve(c.File);s.Charts.Add(c);}Songs.Add(s);}SelectedSong=data["selectedSong"] as string??"";SelectedChart=data["selectedChart"] as string??"";
  }
  public void Save(){var songs=new ArrayList();foreach(var s in Songs){var charts=new ArrayList();foreach(var c in s.Charts)charts.Add(new Hashtable{{"id",c.Id},{"name",c.Name},{"file",c.File}});songs.Add(new Hashtable{{"id",s.Id},{"title",s.Title},{"audio",s.Audio},{"durationMs",s.DurationMs},{"charts",charts}});}var data=new Hashtable{{"version",1},{"selectedSong",SelectedSong},{"selectedChart",SelectedChart},{"songs",songs}};string target=Path.Combine(Root,"library.json"),tmp=target+".tmp";System.IO.File.WriteAllText(tmp,encode(data),Encoding.UTF8);if(System.IO.File.Exists(target))System.IO.File.Replace(tmp,target,target+".bak");else System.IO.File.Move(tmp,target);}
  public SavedSong AddSong(string audioPath,string chartPath,bool persist=true){
   string json=ReadExternalChart(chartPath);var chart=RecordedChart.Parse(decode(json));var wave=WaveData.Read(audioPath,false);chart.CheckAudio(wave.DurationMs);
   string id=Guid.NewGuid().ToString("N"),dir=Resolve(id);Directory.CreateDirectory(dir);
   var s=new SavedSong{Id=id,Title=chart.Title,Audio=id+"/audio.wav",DurationMs=wave.DurationMs};
   System.IO.File.Copy(audioPath,Resolve(s.Audio));var c=WriteChart(s,json,Path.GetFileNameWithoutExtension(chartPath));s.Charts.Add(c);Songs.Add(s);if(persist)Save();return s;
  }
  public SavedChart AddChart(SavedSong song,string path,bool persist=true){string json=ReadExternalChart(path);RecordedChart.Parse(decode(json)).CheckAudio(song.DurationMs);var c=WriteChart(song,json,Path.GetFileNameWithoutExtension(path));song.Charts.Add(c);if(persist)Save();return c;}
  SavedChart WriteChart(SavedSong song,string json,string name){var c=new SavedChart{Id=Guid.NewGuid().ToString("N"),Name=name};c.File=song.Id+"/"+c.Id+".json";System.IO.File.WriteAllText(Resolve(c.File),json,Encoding.UTF8);return c;}
  static string ReadExternalChart(string path){if(new FileInfo(path).Length>3000000)throw new InvalidDataException("谱面不能超过 3 MB");return System.IO.File.ReadAllText(path);}
 }
}
