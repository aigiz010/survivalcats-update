using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// args: modDll dir1;dir2;... (папки игры и рантайма .NET, где искать сборки)
// Проверяет, что каждая ссылка мода на тип/метод/поле из чужой сборки находится в этих сборках с той же сигнатурой.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var modPath = args[0];
var dirs = args[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
var asms = new Dictionary<string, Asm>(StringComparer.OrdinalIgnoreCase);
foreach (var d in dirs)
	foreach (var f in Directory.EnumerateFiles(d, "*.dll"))
	{
		var n = Path.GetFileNameWithoutExtension(f);
		if (asms.ContainsKey(n)) continue;
		try { var a = new Asm(f); if (a.Md != null) asms[a.Name] = a; } catch { }
	}

var mod = new Asm(modPath);
var md = mod.Md;
int bad = 0, checkedRefs = 0;

// 1. Типы
foreach (var h in md.TypeReferences)
{
	var r = Resolver.ResolveTypeRef(mod, h, asms);
	checkedRefs++;
	if (r.asm == null) { bad++; Console.WriteLine("ТИП НЕ НАЙДЕН: " + Names.Ref(md, h)); }
}

// 2. Члены
foreach (var h in md.MemberReferences)
{
	var m = md.GetMemberReference(h);
	string name = md.GetString(m.Name);
	TypeReferenceHandle parentRef;
	if (m.Parent.Kind == HandleKind.TypeReference) parentRef = (TypeReferenceHandle)m.Parent;
	else if (m.Parent.Kind == HandleKind.TypeSpecification)
	{
		var spec = md.GetTypeSpecification((TypeSpecificationHandle)m.Parent);
		var br = md.GetBlobReader(spec.Signature);
		var code = br.ReadSignatureTypeCode();
		if (code != SignatureTypeCode.GenericTypeInstance) continue;
		br.ReadSignatureTypeCode();
		var th = br.ReadTypeHandle();
		if (th.Kind != HandleKind.TypeReference) continue;
		parentRef = (TypeReferenceHandle)th;
	}
	else continue;

	var (asm, td) = Resolver.ResolveTypeRef(mod, parentRef, asms);
	if (asm == null) continue; // уже сообщено
	checkedRefs++;
	var sigProv = new SigProvider();
	bool isField = md.GetBlobReader(m.Signature).ReadSignatureHeader().Kind == SignatureKind.Field;
	string want = isField ? m.DecodeFieldSignature(sigProv, null) : SigProvider.Method(m.DecodeMethodSignature(sigProv, null));

	bool found = false;
	var curAsm = asm; var curTd = td; var seen = new List<string>();
	while (curAsm != null && !found)
	{
		var tdef = curAsm.Md.GetTypeDefinition(curTd);
		if (isField)
		{
			foreach (var fh in tdef.GetFields())
			{
				var f = curAsm.Md.GetFieldDefinition(fh);
				if (curAsm.Md.GetString(f.Name) != name) continue;
				var s = f.DecodeSignature(new SigProvider(), null); seen.Add(s);
				if (s == want) { found = true; break; }
			}
		}
		else
		{
			foreach (var mh in tdef.GetMethods())
			{
				var mdef = curAsm.Md.GetMethodDefinition(mh);
				if (curAsm.Md.GetString(mdef.Name) != name) continue;
				var s = SigProvider.Method(mdef.DecodeSignature(new SigProvider(), null)); seen.Add(s);
				if (s == want) { found = true; break; }
			}
		}
		if (found) break;
		// базовый тип
		var bt = tdef.BaseType;
		if (bt.IsNil) break;
		if (bt.Kind == HandleKind.TypeDefinition) { curTd = (TypeDefinitionHandle)bt; continue; }
		if (bt.Kind == HandleKind.TypeReference) { (curAsm, curTd) = Resolver.ResolveTypeRef(curAsm, (TypeReferenceHandle)bt, asms); continue; }
		if (bt.Kind == HandleKind.TypeSpecification)
		{
			var spec = curAsm.Md.GetTypeSpecification((TypeSpecificationHandle)bt);
			var br = curAsm.Md.GetBlobReader(spec.Signature);
			br.ReadSignatureTypeCode(); br.ReadSignatureTypeCode();
			var th = br.ReadTypeHandle();
			if (th.Kind == HandleKind.TypeDefinition) { curTd = (TypeDefinitionHandle)th; continue; }
			(curAsm, curTd) = Resolver.ResolveTypeRef(curAsm, (TypeReferenceHandle)th, asms); continue;
		}
		break;
	}
	if (!found)
	{
		bad++;
		Console.WriteLine($"ЧЛЕН НЕ НАЙДЕН: {Names.Ref(md, parentRef)}::{name} {want}");
		foreach (var s in seen.Distinct()) Console.WriteLine("    есть: " + s);
	}
}
Console.WriteLine($"проверено ссылок: {checkedRefs}, не найдено: {bad}");
return bad == 0 ? 0 : 1;

sealed class Asm
{
	public readonly string Name; public readonly MetadataReader Md; readonly PEReader pe;
	public readonly Dictionary<string, TypeDefinitionHandle> Types = new();
	public readonly Dictionary<string, AssemblyReferenceHandle> Forwards = new();
	public Asm(string path)
	{
		pe = new PEReader(File.OpenRead(path));
		if (!pe.HasMetadata) return;
		Md = pe.GetMetadataReader();
		if (!Md.IsAssembly) { Md = null; return; }
		Name = Md.GetString(Md.GetAssemblyDefinition().Name);
		foreach (var h in Md.TypeDefinitions) Types[Names.Def(Md, h)] = h;
		foreach (var eh in Md.ExportedTypes)
		{
			var e = Md.GetExportedType(eh);
			if (e.Implementation.Kind == HandleKind.AssemblyReference)
				Forwards[Names.Join(Md.GetString(e.Namespace), Md.GetString(e.Name))] = (AssemblyReferenceHandle)e.Implementation;
		}
	}
}

static class Names
{
	public static string Join(string ns, string n) => string.IsNullOrEmpty(ns) ? n : ns + "." + n;
	public static string Def(MetadataReader md, TypeDefinitionHandle h)
	{
		var t = md.GetTypeDefinition(h);
		var dt = t.GetDeclaringType();
		if (!dt.IsNil) return Def(md, dt) + "/" + md.GetString(t.Name);
		return Join(md.GetString(t.Namespace), md.GetString(t.Name));
	}
	public static string Ref(MetadataReader md, TypeReferenceHandle h)
	{
		var t = md.GetTypeReference(h);
		if (t.ResolutionScope.Kind == HandleKind.TypeReference) return Ref(md, (TypeReferenceHandle)t.ResolutionScope) + "/" + md.GetString(t.Name);
		return Join(md.GetString(t.Namespace), md.GetString(t.Name));
	}
}

static class Resolver
{
	public static (Asm asm, TypeDefinitionHandle td) ResolveTypeRef(Asm from, TypeReferenceHandle h, Dictionary<string, Asm> asms)
	{
		var md = from.Md;
		var t = md.GetTypeReference(h);
		string full = Names.Ref(md, h);
		Asm target;
		switch (t.ResolutionScope.Kind)
		{
			case HandleKind.AssemblyReference:
				var ar = md.GetAssemblyReference((AssemblyReferenceHandle)t.ResolutionScope);
				if (!asms.TryGetValue(md.GetString(ar.Name), out target)) return (null, default);
				break;
			case HandleKind.TypeReference:
				var root = full.Split('/')[0];
				var outer = (TypeReferenceHandle)t.ResolutionScope;
				while (md.GetTypeReference(outer).ResolutionScope.Kind == HandleKind.TypeReference) outer = (TypeReferenceHandle)md.GetTypeReference(outer).ResolutionScope;
				var (oa, _) = ResolveTypeRef(from, outer, asms);
				if (oa == null) return (null, default);
				target = oa;
				break;
			case HandleKind.ModuleDefinition:
				target = from; break;
			default: return (null, default);
		}
		for (int hop = 0; hop < 5; hop++)
		{
			if (target.Types.TryGetValue(full, out var td)) return (target, td);
			var root = full.Split('/')[0];
			if (target.Forwards.TryGetValue(root, out var fwd))
			{
				var name = target.Md.GetString(target.Md.GetAssemblyReference(fwd).Name);
				if (!asms.TryGetValue(name, out target)) return (null, default);
				continue;
			}
			return (null, default);
		}
		return (null, default);
	}
}

sealed class SigProvider : ISignatureTypeProvider<string, object>
{
	public static string Method(MethodSignature<string> s) =>
		(s.Header.IsInstance ? "instance " : "") + s.ReturnType + " " + (s.GenericParameterCount > 0 ? "<" + s.GenericParameterCount + ">" : "") + "(" + string.Join(", ", s.ParameterTypes) + ")";
	public string GetArrayType(string e, ArrayShape shape) => e + "[" + new string(',', shape.Rank - 1) + "]";
	public string GetByReferenceType(string e) => e + "&";
	public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
	public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
	public string GetGenericMethodParameter(object c, int i) => "!!" + i;
	public string GetGenericTypeParameter(object c, int i) => "!" + i;
	public string GetModifiedType(string m, string u, bool req) => u + (req ? " modreq(" : " modopt(") + m + ")";
	public string GetPinnedType(string e) => e + " pinned";
	public string GetPointerType(string e) => e + "*";
	public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString();
	public string GetSZArrayType(string e) => e + "[]";
	public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => Names.Def(r, h);
	public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => Names.Ref(r, h);
	public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
}
