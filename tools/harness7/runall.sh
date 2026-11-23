#!/usr/bin/env bash
# runall.sh <mod.dll> — патчи мода на каждой версии игры, на её версии .NET
D="$(cd "$(dirname "$0")" && pwd -W)"
H="$D/bin/Release/net7.0/harness7.dll"
G="$D/../../gamerefs"
rc=0
for v in 1.18.8 1.18.15 1.19.8 1.20.12 1.21.6 1.22.7 1.23.0-pre.1; do
	case $v in
		1.20.12|1.21.6) net=8.0.19 ;; # 1.20.12: Harmony тянет System.Text.Json 8 (в игре — из Lib по deps.json)
		1.22.7|1.23.0-pre.1) net=10.0.15 ;;
		*) net=7.0.10 ;;
	esac
	P="$G/$v"
	echo "======== $v (.NET $net)"
	unset HARMONY_DIR
	dotnet exec --fx-version $net "$H" "$P" "$1" || rc=1
done
exit $rc
