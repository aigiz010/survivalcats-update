import sys, zipfile, os
src, out = sys.argv[1], sys.argv[2]
files = [("modinfo.json", os.path.join(src, "modinfo.json")),
         ("modicon.png", os.path.join(src, "modicon.png")),
         ("survivalcats_update.dll", os.path.join(src, "bin/Release/net7.0/survivalcats_update.dll")),
         ("survivalcats_update.pdb", os.path.join(src, "bin/Release/net7.0/survivalcats_update.pdb"))]
for root, _, names in os.walk(os.path.join(src, "assets")):
    for n in names:
        p = os.path.join(root, n)
        files.append((os.path.relpath(p, src).replace("\\", "/"), p))
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for arc, p in files:
        z.write(p, arc)
print(out)
