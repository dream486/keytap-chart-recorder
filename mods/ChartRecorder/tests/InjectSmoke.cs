using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class InjectSmoke {
 static void Main(string[] args){var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(args[0]));var module=ModuleDefinition.ReadModule(args[0],new ReaderParameters{AssemblyResolver=resolver});var probe=ModuleDefinition.ReadModule(args[1]);var init=module.ImportReference(probe.Types.Single(t=>t.Name=="ModSmokeProbe").Methods.Single(m=>m.Name=="Initialize"));var awake=module.Types.Single(t=>t.Name=="SteamManager").Methods.Single(m=>m.Name=="Awake");awake.Body.GetILProcessor().InsertBefore(awake.Body.Instructions[0],Instruction.Create(OpCodes.Call,init));
  foreach(var type in module.Types)foreach(var method in type.Methods){bool disable=(type.Name.EndsWith("_SongPlayer")&&(method.Name=="OnApplicationFocus"||method.Name=="OnApplicationPause"))||(type.Name=="UserMemory"&&(method.Name=="Save"||method.Name=="InstanceSave"||method.Name=="SaveToPlayerPrefs"||method.Name=="AddPlayCount"))||(type.Name=="SteamAchievement"&&(method.Name=="RefreshStat"||method.Name=="UnlockAchievement"||method.Name=="SaveTrophyProgress"));if(disable&&method.HasBody&&method.ReturnType.FullName=="System.Void"){method.Body=new MethodBody(method);method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));}}
  module.Write(args[2]);Console.WriteLine("Smoke scenario injected only in game test copy");
 }
}
