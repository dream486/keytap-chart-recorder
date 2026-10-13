using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class InjectProbe {
 static void Main(string[] args){
  var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(args[0]));
  var module=ModuleDefinition.ReadModule(args[0],new ReaderParameters{AssemblyResolver=resolver});
  var probe=ModuleDefinition.ReadModule(args[1]);
  var init=module.ImportReference(probe.Types.First(t=>t.Name=="RuntimeProbe").Methods.First(m=>m.Name=="Initialize"));
  var awake=module.Types.First(t=>t.Name=="SteamManager").Methods.First(m=>m.Name=="Awake");
  awake.Body.GetILProcessor().InsertBefore(awake.Body.Instructions[0],Instruction.Create(OpCodes.Call,init));
  foreach(var type in module.Types)foreach(var method in type.Methods){
   bool disable=(type.Name=="SongInfoCore"&&(method.Name=="UploadScore"||method.Name=="UpdateSync"))
     ||(type.Name=="UserMemory"&&(method.Name=="Save"||method.Name=="InstanceSave"||method.Name=="SaveToPlayerPrefs"||method.Name=="AddPlayCount"))
     ||(type.Name=="SteamAchievement"&&(method.Name=="RefreshStat"||method.Name=="UnlockAchievement"||method.Name=="SaveTrophyProgress"));
   if(disable && method.ReturnType.FullName=="System.Void" && method.HasBody){method.Body=new MethodBody(method);method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));Console.WriteLine("Disabled "+type.Name+"."+method.Name);}
  }
  module.Write(args[2]); Console.WriteLine("Runtime validation probe injected in test copy only");
 }
}
