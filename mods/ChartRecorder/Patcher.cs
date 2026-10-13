using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Patcher {
 static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in AllTypes(t.NestedTypes))yield return n;}}
 static void Main(string[] args){
  if(args.Length!=3)throw new Exception("Patcher original-Assembly-CSharp.dll ChartRecorderMod.dll output.dll");
  var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(args[0]));resolver.AddSearchDirectory(Path.GetDirectoryName(args[1]));
  var module=ModuleDefinition.ReadModule(args[0],new ReaderParameters{AssemblyResolver=resolver});
  if(module.AssemblyReferences.Any(r=>r.Name=="ChartRecorderMod"))throw new Exception("This assembly already contains the mod; use the verified original backup");
  var mod=ModuleDefinition.ReadModule(args[1]);var api=mod.Types.Single(t=>t.FullName=="ChartRecorder.GameChartMod");
  Func<string,MethodReference> hook=name=>module.ImportReference(api.Methods.Single(m=>m.Name==name));
  Action<MethodDefinition,string,bool> objectHook=(method,name,before)=>{
   var il=method.Body.GetILProcessor();var target=hook(name);
   if(before){var first=method.Body.Instructions[0];il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_0));il.InsertBefore(first,Instruction.Create(OpCodes.Call,target));}
   else foreach(var ret in method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){
    // Convert ret in place so existing branches to it execute the hook as well.
    ret.OpCode=OpCodes.Ldarg_0;ret.Operand=null;var call=Instruction.Create(OpCodes.Call,target);il.InsertAfter(ret,call);il.InsertAfter(call,Instruction.Create(OpCodes.Ret));
   }
  };
  var steam=module.Types.Single(t=>t.Name=="SteamManager").Methods.Single(m=>m.Name=="Awake");steam.Body.GetILProcessor().InsertBefore(steam.Body.Instructions[0],Instruction.Create(OpCodes.Call,hook("Initialize")));
  var getText=module.Types.Single(t=>t.Name=="AssetBundleManager").Methods.Single(m=>m.Name=="GetBmsText");getText.Body=new MethodBody(getText);getText.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));getText.Body.Instructions.Add(Instruction.Create(OpCodes.Call,hook("GetChart")));getText.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
  var core=module.Types.Single(t=>t.Name=="SongInfoCore");
  var create=core.Methods.Single(m=>m.Name=="Create");foreach(var ret in create.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){
   var il=create.Body.GetILProcessor();ret.OpCode=OpCodes.Dup;ret.Operand=null;var call=Instruction.Create(OpCodes.Call,hook("ConfigureSongInfo"));il.InsertAfter(ret,call);il.InsertAfter(call,Instruction.Create(OpCodes.Ret));
  }
  foreach(string name in new[]{"UploadScore"}){
   var m=core.Methods.Single(x=>x.Name==name);var first=m.Body.Instructions[0];var il=m.Body.GetILProcessor();foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("IsCustomSlot")),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ret)})il.InsertBefore(first,i);
  }
  var sync=core.Methods.Single(m=>m.Name=="UpdateSync");var syncFirst=sync.Body.Instructions[0];foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("UpdateCustomScore")),Instruction.Create(OpCodes.Brfalse,syncFirst),Instruction.Create(OpCodes.Ret)})sync.Body.GetILProcessor().InsertBefore(syncFirst,i);
  var best=core.Methods.Single(m=>m.Name=="get_SyncNumber");foreach(var ret in best.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){var il=best.Body.GetILProcessor();var local=new VariableDefinition(module.TypeSystem.Int32);best.Body.Variables.Add(local);ret.OpCode=OpCodes.Stloc;ret.Operand=local;var a=Instruction.Create(OpCodes.Ldarg_0);il.InsertAfter(ret,a);var b=Instruction.Create(OpCodes.Ldloc,local);il.InsertAfter(a,b);var c=Instruction.Create(OpCodes.Call,hook("BestSync"));il.InsertAfter(b,c);il.InsertAfter(c,Instruction.Create(OpCodes.Ret));}
  foreach(string noteName in new[]{"BMSLib.Note","BMSLib.LongNote"}){var knock=module.Types.Single(t=>t.FullName==noteName).Methods.Single(m=>m.Name=="Knock");foreach(var ret in knock.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){var il=knock.Body.GetILProcessor();ret.OpCode=OpCodes.Ldarg_0;ret.Operand=null;var c=Instruction.Create(OpCodes.Call,hook("AcceptedHit"));il.InsertAfter(ret,c);il.InsertAfter(c,Instruction.Create(OpCodes.Ret));}}
  foreach(string typeName in new[]{"SongInfoPackageScript","SmallSongInfoPackageScript"}){
   var reset=module.Types.Single(t=>t.Name==typeName).Methods.Single(m=>m.Name=="Reset");var first=reset.Body.Instructions[0];var il=reset.Body.GetILProcessor();il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_1));il.InsertBefore(first,Instruction.Create(OpCodes.Call,hook("ConfigureSongInfo")));
  }
  foreach(string typeName in new[]{"SelectSongController","MoveSongScript"}){
   var update=module.Types.Single(t=>t.Name==typeName).Methods.Single(m=>m.Name=="Update");var first=update.Body.Instructions[0];var il=update.Body.GetILProcessor();foreach(var i in new[]{Instruction.Create(OpCodes.Call,hook("BlockMenuInput")),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ret)})il.InsertBefore(first,i);
  }
  int players=0,calls=0;
  foreach(var t in module.Types.Where(t=>t.Name.EndsWith("_SongPlayer"))){
   var awake=t.Methods.FirstOrDefault(m=>m.Name=="Awake");var update=t.Methods.FirstOrDefault(m=>m.Name=="Update");var play=t.Methods.FirstOrDefault(m=>m.Name=="PlayWAVById");var pause=t.Methods.FirstOrDefault(m=>m.Name=="PlayPause");
   if(awake==null||update==null||play==null)continue;objectHook(awake,"PlayerReady",false);objectHook(update,"SyncPlayer",true);if(pause!=null)objectHook(pause,"PausePlayer",false);
   var first=play.Body.Instructions[0];var il=play.Body.GetILProcessor();foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("TryPlayBackground")),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Ret)})il.InsertBefore(first,i);players++;
   foreach(var child in AllTypes(new[]{t}))foreach(var method in child.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions){
    var target=instruction.Operand as MethodReference;if(target==null||target.DeclaringType.Name!="CriAtomSource"||target.Name!="Play"||target.Parameters.Count!=0)continue;
    instruction.OpCode=OpCodes.Call;instruction.Operand=hook("StartCri");calls++;
   }
  }
  if(players<10||calls<10)throw new Exception("Unsupported MUSYNX assembly layout");
  // Added instructions can push an existing short branch past its signed-byte range.
  var opcodes=typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Name);
  foreach(var type in AllTypes(module.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions)
   if(instruction.OpCode.OperandType==OperandType.ShortInlineBrTarget)instruction.OpCode=opcodes[instruction.OpCode.Name.Substring(0,instruction.OpCode.Name.Length-2)];
  module.Write(args[2]);Console.WriteLine("Patched "+players+" native player classes and "+calls+" background resume calls; runtime library hook installed");
 }
}
