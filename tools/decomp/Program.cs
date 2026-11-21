using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;

// decomp <dll> <outDir> [refDir...]
string dll = args[0];
string outDir = args[1];
Directory.CreateDirectory(outDir);
var file = new PEFile(dll);
var resolver = new UniversalAssemblyResolver(dll, false, file.DetectTargetFrameworkId());
foreach (var d in args.Skip(2)) resolver.AddSearchDirectory(d);
var settings = new DecompilerSettings { ThrowOnAssemblyResolveErrors = false };
var dec = new CSharpDecompiler(file, resolver, settings);
foreach (var t in dec.TypeSystem.MainModule.TypeDefinitions)
{
	if (t.DeclaringTypeDefinition != null) continue;
	if (t.Name == "<Module>") continue;
	string name = (t.Namespace + "." + t.Name).Trim('.');
	foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
	try
	{
		File.WriteAllText(Path.Combine(outDir, name + ".cs"), dec.DecompileTypeAsString(t.FullTypeName));
	}
	catch (Exception e)
	{
		File.WriteAllText(Path.Combine(outDir, name + ".err.txt"), e.ToString());
	}
}
Console.WriteLine("done: " + Directory.GetFiles(outDir).Length);
