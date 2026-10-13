using System;
using System.IO;
using System.Collections;
using SonicAudioLib.CriMw;
using SonicAudioLib.Archives;
class BankTool {
 static void Main(string[] a) {
  if(a[0]=="api") { foreach(var t in typeof(CriTable).Assembly.GetTypes()) if(t.Name=="CriTable"||t.Name=="CriAfs2Archive"||t.Name=="CriAfs2Entry") { Console.WriteLine(t.FullName); foreach(var m in t.GetMembers()) Console.WriteLine(m); } return; }
  var acb=new CriTable(); acb.Load(a[1]);
  if(a[0]=="replace") {
   var wave=new CriTable(); wave.Load((byte[])acb.Rows[0]["WaveformTable"]);
   if(wave.Rows.Count!=1) throw new Exception("Expected one waveform for this test slot");
   var row=wave.Rows[0];
   row["NumSamples"]=(uint)1058400; row["SamplingRate"]=(ushort)44100;
   row["NumChannels"]=(byte)2; row["LoopFlag"]=(byte)0;
   wave.WriterSettings=CriTableWriterSettings.Adx2Settings;
   acb.Rows[0]["WaveformTable"]=wave.Save();
   var cue=new CriTable(); cue.Load((byte[])acb.Rows[0]["CueTable"]);
   cue.Rows[0]["Length"]=(uint)24000;
   cue.WriterSettings=CriTableWriterSettings.Adx2Settings;
   acb.Rows[0]["CueTable"]=cue.Save();
   var awb=new CriAfs2Archive(); awb.Load((byte[])acb.Rows[0]["AwbFile"]);
   var id=awb[0].Id; awb.Clear();
   awb.Add(new CriAfs2Entry { Id=id, FilePath=new FileInfo(a[3]) });
   acb.Rows[0]["AwbFile"]=awb.Save();
   acb.WriterSettings=CriTableWriterSettings.Adx2Settings;
   acb.Save(a[2]); Console.WriteLine("Replaced cue lazhuren: 24 s, stereo 44100 Hz, non-looping"); return;
  }
  foreach(var key in new[]{"CueTable","CueNameTable","WaveformTable","SynthTable"}) {
   var table=new CriTable(); table.Load((byte[])acb.Rows[0][key]);
   Console.WriteLine(key);
   foreach(var row in table.Rows) { foreach(var field in table.Fields) { var value=row[field.FieldName]; Console.Write(field.FieldName+"="+(value is byte[] ? BitConverter.ToString((byte[])value): value)+" "); } Console.WriteLine(); }
  }
  File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(a[1]),"original.awb"),(byte[])acb.Rows[0]["AwbFile"]);
 }
}
