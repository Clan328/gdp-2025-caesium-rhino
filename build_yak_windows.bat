
dotnet build -c Release
copy manifest.yml LoadTiles\bin\Release\net7.0
pushd LoadTiles\bin\Release\net7.0\
"C:\Program Files\Rhino 8\System\Yak.exe" build
popd
move LoadTiles\bin\Release\net7.0\sealion-1.1.0-rh8_0-any.yak .\sealion-1.1.0-rh8_0-windows.yak

