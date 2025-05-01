#!/bin/bash

dotnet build -c Release
cp manifest.yml LoadTiles/bin/Release/net7.0
cd LoadTiles/bin/Release/net7.0/
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" build # Use "C:\Program Files\Rhino 8\System\Yak.exe" in Windows
cd ../../../../
mv LoadTiles/bin/Release/net7.0/cesiumion-1.0.0-rh8_0-any.yak ./cesiumion-1.0.0-rh8_0-any.yak
