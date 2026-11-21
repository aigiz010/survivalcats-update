#!/usr/bin/env bash
# checkall.sh <mod.dll> — сверка ссылок мода со всеми версиями игры
D="$(cd "$(dirname "$0")" && pwd -W)"
C="$D/bin/Release/net8.0/compatcheck.exe"
R="C:/Program Files/dotnet/shared/Microsoft.NETCore.App"
G="$D/../../gamerefs"
rc=0
for v in 1.18.8 1.18.15 1.19.8 1.20.12 1.21.6 1.22.7; do
	case $v in
		1.21.6) net=8.0.19 ;;
		1.22.7) net=10.0.15 ;;
		*) net=7.0.10 ;;
	esac
	P="$G/$v"
	echo "== $v (.NET $net)"
	"$C" "$1" "$P;$P/Lib;$P/Mods;$R/$net" || rc=1
done
exit $rc
