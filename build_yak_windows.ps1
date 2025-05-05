
dotnet build -c Release
cp manifest.yml LoadTiles\bin\Release\net7.0
Push-Location LoadTiles\bin\Release\net7.0\
& 'C:\Program Files\Rhino 8\System\Yak.exe' build
Pop-Location
mv LoadTiles\bin\Release\net7.0\cesiumion-1.0.0-rh8_0-any.yak .\cesiumion-1.0.0-rh8_0-any.yak

