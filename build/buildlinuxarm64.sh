dotnet publish ../omtplayer.sln -r linux-arm64 -c Release
mkdir arm64
cp ../bin/Release/net8.0/linux-arm64/native/omtplayer ./arm64/omtplayer
cp ../../libvmx/build/libvmx.so ./arm64/libvmx.so