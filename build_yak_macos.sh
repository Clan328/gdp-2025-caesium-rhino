#!/bin/bash

dotnet build -c Release
cp manifest.yml LoadTiles/bin/Release/net7.0
pushd LoadTiles/bin/Release/net7.0/
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" build
popd
mv LoadTiles/bin/Release/net7.0/sealion-1.0.1-rh8_0-any.yak ./sealion-1.0.1-rh8_0-mac.yak

