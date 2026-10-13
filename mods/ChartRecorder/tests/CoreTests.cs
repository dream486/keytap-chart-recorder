using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using ChartRecorder;
class CoreTests {
 static int count;static JavaScriptSerializer json=new JavaScriptSerializer();
 static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 static void Reject(Action action,string name){try{action();}catch(InvalidDataException){Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
 static void Main(string[] args){
  string fixture=File.ReadAllText(args[0]);var chart=RecordedChart.Parse(json.DeserializeObject(fixture));Check(chart.Notes.Count==28&&chart.Notes.Count(n=>n.End.HasValue)==4,"tap/chord/hold fixture parsed");
  string exported=chart.ToMusynx(24000);Check(exported.Contains("LongNote\t120000000\t3\t01\t140000000")&&exported.Contains("Note\t100000000\t7\t01"),"hold endpoints and four-key chords preserve song time");
  Reject(()=>RecordedChart.Parse(json.DeserializeObject(fixture.Replace("\"keys\": 4","\"keys\": 6"))),"wrong key count rejected");
  Reject(()=>chart.CheckAudio(1000),"audio shorter than notes rejected");
  Reject(()=>RecordedChart.Parse(json.DeserializeObject("{\"version\":1,\"keys\":4,\"title\":\"x\",\"bpm\":120,\"duration\":3000,\"notes\":[{\"lane\":0,\"time\":1000,\"endTime\":2000},{\"lane\":0,\"time\":1500}]}")),"overlapping notes rejected");
  var wave=WaveData.Read(args[1],true);Check(wave.DurationMs==24000&&wave.Channels==2&&wave.Samples.Length==1058400*2,"PCM audio samples and duration verified");
  var lib=new Library(args[2],json.DeserializeObject,json.Serialize);var song=lib.AddSong(args[1],args[0]);var other=lib.AddChart(song,args[0]);Check(song.Charts.Count==2,"one song holds multiple charts");
  lib.SelectedSong=song.Id;lib.SelectedChart=other.Id;lib.Save();var reload=new Library(args[2],json.DeserializeObject,json.Serialize);reload.Load();Check(reload.Songs.Count==1&&reload.Songs[0].Charts.Count==2&&reload.SelectedChart==other.Id,"library and chosen chart survive reload");
  Reject(()=>lib.Resolve("../outside.wav"),"library traversal rejected");Reject(()=>lib.Resolve(Path.GetFullPath(args[0])),"absolute library paths rejected");
  Check(lib.ReadChart(song.Charts[0].File).Notes.Count==28,"copied chart reads independently of source");
  var progress=new ProgressStore(Path.Combine(args[2],"scores.json"),json.DeserializeObject,json.Serialize);
  progress.Record(song.Id,song.Charts[0].Id,new PlayResult{Sync=12123,MaxCombo=32,Ex=22,Exact=10,At="first",Scene="UIB_PlayingScene"});
  Check(!progress.Record(song.Id,song.Charts[0].Id,new PlayResult{Sync=11000,MaxCombo=20,At="second"}),"lower score retains highest native score");
  progress.Record(song.Id,other.Id,new PlayResult{Sync=9000,At="third"});progress.Record("another-song",song.Charts[0].Id,new PlayResult{Sync=8000});
  Check(progress.Entries.Count==3&&progress.Get(song.Id,song.Charts[0].Id).Best.Sync==12123,"scores isolated by song and chart IDs");
  progress.HitSound=0;progress.HitVolume=.25f;progress.Save();var saved=new ProgressStore(Path.Combine(args[2],"scores.json"),json.DeserializeObject,json.Serialize);saved.Load();var entry=saved.Get(song.Id,song.Charts[0].Id);
  Check(entry.Plays==2&&entry.Last.Sync==11000&&entry.Best.Ex==22&&entry.History.Count==2&&saved.HitSound==0&&saved.HitVolume==.25f,"judgements, history and audio preferences survive reload");
  for(int i=0;i<25;i++)saved.Record(song.Id,other.Id,new PlayResult{Sync=i});Check(saved.Get(song.Id,other.Id).History.Count==20&&saved.Get(song.Id,other.Id).Plays==26,"recent history bounded while total play count retained");
  Check(File.Exists(Path.Combine(args[2],"scores.json.bak")),"atomic score save retains previous index backup");
  Console.WriteLine(count+" checks passed");
 }
}
