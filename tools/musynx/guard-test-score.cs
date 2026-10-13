// Keep the replacement slot out of the original song's local/online scores.
// Requires Mono.Cecil; does not add a runtime DLL or change other song behavior.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class GuardTestScore {
 static void Main(string[] args) {
  var resolver=new DefaultAssemblyResolver();
  resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(args[0]));
  var module=ModuleDefinition.ReadModule(args[0],new ReaderParameters{AssemblyResolver=resolver});
  var core=module.Types.First(t=>t.Name=="SongInfoCore");
  var field=core.Fields.First(f=>f.Name=="bmsCoreName");
  var equals=module.ImportReference(typeof(string).GetMethod("op_Equality",new[]{typeof(string),typeof(string)}));
  foreach(var name in new[]{"UpdateSync","UploadScore"}) {
   var method=core.Methods.Single(m=>m.Name==name);
   if(method.IsStatic||method.ReturnType.FullName!="System.Void")throw new Exception("Unsupported game version");
   var il=method.Body.GetILProcessor();var first=method.Body.Instructions[0];
   foreach(var instruction in new[]{
    Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,field),
    Instruction.Create(OpCodes.Ldstr,"lazhuren"),Instruction.Create(OpCodes.Call,equals),
    Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ret)
   }) il.InsertBefore(first,instruction);
   Console.WriteLine("Guarded replacement slot: SongInfoCore."+name);
  }
  module.Write(args[1]);
 }
}
