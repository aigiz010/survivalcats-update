using System.Reflection;
using System.Runtime.Loader;
// args: gameDir modDll — применяет патчи мода к сборкам игры так же, как мод (по классу), и печатает итог.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var game = args[0]; var modDll = args[1];
string[] dirs = { game, Path.Combine(game, "Lib"), Path.Combine(game, "Mods") };
AssemblyLoadContext.Default.Resolving += (ctx, name) => {
  foreach (var d in dirs) { var p = Path.Combine(d, name.Name + ".dll"); if (File.Exists(p)) return ctx.LoadFromAssemblyPath(p); }
  return null;
};
Console.WriteLine("Runtime " + Environment.Version);
var hdir = Environment.GetEnvironmentVariable("HARMONY_DIR"); if (hdir != null) dirs = new[] { hdir, game, Path.Combine(game, "Lib"), Path.Combine(game, "Mods") };
var harmonyAsm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hdir ?? Path.Combine(game, "Lib"), "0Harmony.dll"));
Console.WriteLine("Harmony " + harmonyAsm.GetName().Version);
foreach (var n in new[] { "VintagestoryAPI.dll", "Mods/VSEssentials.dll", "Mods/VSSurvivalMod.dll" })
  AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(game, n)));
var mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(modDll));
var harmonyType = harmonyAsm.GetType("HarmonyLib.Harmony");
var patchAttr = harmonyAsm.GetType("HarmonyLib.HarmonyPatch");
var h = Activator.CreateInstance(harmonyType, "test.survivalcats");
var createProc = harmonyType.GetMethod("CreateClassProcessor", new[] { typeof(Type) });
int failed = 0;
foreach (var t in mod.GetTypes())
{
  if (!t.IsDefined(patchAttr, false)) continue;
  try { var proc = createProc.Invoke(h, new object[] { t }); proc.GetType().GetMethod("Patch").Invoke(proc, null); }
  catch (TargetInvocationException e) { failed++; var ex = e.InnerException; while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException; Console.WriteLine("НЕ ПРИМЕНЁН " + t.Name + ": " + ex.GetType().Name + ": " + ex.Message + (Environment.GetEnvironmentVariable("HV") != null ? Environment.NewLine + ex : "")); }
}
var patched = (IEnumerable<MethodBase>)harmonyType.GetMethod("GetPatchedMethods").Invoke(h, null);
foreach (var m in patched.OrderBy(m => m.DeclaringType.Name + m.Name)) Console.WriteLine("  patched: " + m.DeclaringType.Name + "." + m.Name);
Console.WriteLine("патчей: " + patched.Count() + ", не применено классов: " + failed);
foreach (var s in new[] { "PatcherinitDetailGui", "PatcherHandbookVerticalTabs", "PatcherRichtextItemZ", "PatcherSearchClearButton" })
  Console.WriteLine("  " + mod.GetType("survivalcats.src." + s).GetField("Status").GetValue(null));
var compat = mod.GetType("survivalcats.src.Compat");
foreach (var f in compat.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
{
  var v = f.GetValue(null);
  Console.WriteLine("  Compat." + f.Name + " = " + (v is MemberInfo mi ? (mi is Type ty ? ty.FullName : mi.DeclaringType.Name + "." + mi.Name) : v?.ToString() ?? "NULL"));
}
foreach (var cls in new[] { "Bookmarks", "HandbookWindowSync" })
{
  var ty = mod.GetType("survivalcats.src." + cls);
  System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(ty.TypeHandle);
  foreach (var f in ty.GetFields(BindingFlags.NonPublic | BindingFlags.Static).Where(f => !f.FieldType.IsPrimitive && f.FieldType != typeof(string) && (f.FieldType.Name.Contains("FieldRef") || typeof(MemberInfo).IsAssignableFrom(f.FieldType))))
    Console.WriteLine("  " + cls + "." + f.Name + " = " + (f.GetValue(null) != null ? "есть" : "NULL"));
}
// Все методы мода JIT-компилируются: отсутствующий член игры дал бы MissingMethod/MissingField/TypeLoad
int jitFail = 0;
foreach (var t in mod.GetTypes())
  foreach (var m in t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
  {
    if (m.IsAbstract || m.ContainsGenericParameters) continue;
    try { System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); }
    catch (Exception e) { jitFail++; Console.WriteLine("JIT " + t.Name + "." + m.Name + ": " + e.GetType().Name + ": " + e.Message); }
  }
Console.WriteLine("JIT ошибок: " + jitFail);
return failed + jitFail == 0 ? 0 : 1;
