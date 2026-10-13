using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using System.Collections.Generic;
using System.Web.Script.Serialization;
// Separate desktop helper: dialogs and Windows audio codecs stay out of Unity Mono.
class ImportHelper {
 [STAThread] static int Main(string[] args){
  if(args.Length<2)return 2;string result=args[1];var output=new Dictionary<string,object>();
  try{
   string mode=args[0],audio="",chart="";
   if(mode=="song"){
    audio=args.Length>2?args[2]:Pick("选择歌曲音乐","音乐文件 (*.mp3;*.wav)|*.mp3;*.wav");if(audio==null){Write(result,new Dictionary<string,object>{{"cancelled",true}});return 0;}
    chart=args.Length>3?args[3]:Pick("选择敲谱器导出的 JSON 谱面","敲谱器谱面 (*.json)|*.json");if(chart==null){Write(result,new Dictionary<string,object>{{"cancelled",true}});return 0;}
    if(new FileInfo(audio).Length>100000000)throw new Exception("音乐不能超过 100 MB");
    string destination=Path.Combine(Path.GetDirectoryName(result),Path.GetFileNameWithoutExtension(result)+".wav");
    using(var reader=new AudioFileReader(audio)){
     if(reader.TotalTime.TotalMilliseconds<=0||reader.TotalTime.TotalMilliseconds>600000)throw new Exception("歌曲最长支持 10 分钟");
     if(reader.WaveFormat.Channels>2)throw new Exception("请选择单声道或双声道音乐");
     WaveFileWriter.CreateWaveFile16(destination,reader);
    }
    output["audio"]=destination;output["sourceAudio"]=audio;
   }else if(mode=="chart"){
    chart=args.Length>2?args[2]:Pick("添加另一张敲谱器 JSON 谱面","敲谱器谱面 (*.json)|*.json");if(chart==null){Write(result,new Dictionary<string,object>{{"cancelled",true}});return 0;}
   }else throw new Exception("未知导入操作");
   if(new FileInfo(chart).Length>3000000)throw new Exception("谱面不能超过 3 MB");output["chart"]=chart;output["ok"]=true;Write(result,output);return 0;
  }catch(Exception e){Write(result,new Dictionary<string,object>{{"error",e.Message}});return 1;}
 }
 static string Pick(string title,string filter){using(var dialog=new OpenFileDialog{Title=title,Filter=filter,CheckFileExists=true,Multiselect=false})return dialog.ShowDialog()==DialogResult.OK?dialog.FileName:null;}
 static void Write(string result,object value){Directory.CreateDirectory(Path.GetDirectoryName(result));File.WriteAllText(result,new JavaScriptSerializer().Serialize(value),Encoding.UTF8);}
}
