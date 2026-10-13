using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace ChartRecorder {
 public sealed class PlayResult {
  public int Sync,MaxCombo,Ex,Exact,Great,Right,Miss; public string At,Scene;
  public IDictionary Data(){return new Hashtable{{"sync",Sync},{"maxCombo",MaxCombo},{"ex",Ex},{"exact",Exact},{"great",Great},{"right",Right},{"miss",Miss},{"at",At},{"scene",Scene}};}
  public static PlayResult Read(IDictionary d){if(d==null)return null;return new PlayResult{Sync=Convert.ToInt32(d["sync"]),MaxCombo=Convert.ToInt32(d["maxCombo"]),Ex=Convert.ToInt32(d["ex"]),Exact=Convert.ToInt32(d["exact"]),Great=Convert.ToInt32(d["great"]),Right=Convert.ToInt32(d["right"]),Miss=Convert.ToInt32(d["miss"]),At=d["at"] as string,Scene=d["scene"] as string};}
 }
 public sealed class ChartProgress {
  public string SongId,ChartId;public int Plays;public PlayResult Best,Last;public List<PlayResult> History=new List<PlayResult>();
 }
 public sealed class ProgressStore {
  public readonly List<ChartProgress> Entries=new List<ChartProgress>();public int HitSound=1;public float HitVolume=.5f;
  readonly string path;readonly Func<string,object> decode;readonly Func<object,string> encode;
  public ProgressStore(string file,Func<string,object> decodeJson,Func<object,string> encodeJson){path=file;decode=decodeJson;encode=encodeJson;}
  public ChartProgress Get(string song,string chart){return Entries.Find(e=>e.SongId==song&&e.ChartId==chart);}
  public bool Record(string song,string chart,PlayResult result){var e=Get(song,chart);bool fresh=e==null||e.Best==null||result.Sync>e.Best.Sync;if(e==null){e=new ChartProgress{SongId=song,ChartId=chart};Entries.Add(e);}e.Plays++;e.Last=result;if(fresh)e.Best=result;e.History.Insert(0,result);if(e.History.Count>20)e.History.RemoveRange(20,e.History.Count-20);Save();return fresh;}
  public void Load(){if(!File.Exists(path))return;var d=decode(File.ReadAllText(path)) as IDictionary;if(d==null||Convert.ToInt32(d["version"])!=1)throw new InvalidDataException("成绩索引格式无效");HitSound=Math.Max(0,Math.Min(2,Convert.ToInt32(d["hitSound"])));HitVolume=Math.Max(0,Math.Min(1,Convert.ToSingle(d["hitVolume"])));foreach(IDictionary item in (IList)d["scores"]){var e=new ChartProgress{SongId=(string)item["songId"],ChartId=(string)item["chartId"],Plays=Convert.ToInt32(item["plays"]),Best=PlayResult.Read(item["best"] as IDictionary),Last=PlayResult.Read(item["last"] as IDictionary)};foreach(IDictionary r in (IList)item["history"])e.History.Add(PlayResult.Read(r));Entries.Add(e);}}
  public void Save(){var list=new ArrayList();foreach(var e in Entries){var h=new ArrayList();foreach(var r in e.History)h.Add(r.Data());list.Add(new Hashtable{{"songId",e.SongId},{"chartId",e.ChartId},{"plays",e.Plays},{"best",e.Best==null?null:e.Best.Data()},{"last",e.Last==null?null:e.Last.Data()},{"history",h}});}var d=new Hashtable{{"version",1},{"hitSound",HitSound},{"hitVolume",HitVolume},{"scores",list}};Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,encode(d),Encoding.UTF8);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
 }
}
