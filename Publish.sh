#!/bin/bash
mkdir -p Publish/bin
dotnet publish IntelliTrader/IntelliTrader.csproj -f net8.0 -c Release -o "Publish/bin"
dotnet publish IntelliTrader.Web/IntelliTrader.Web.csproj -f net8.0 -c Release -o "Publish/bin"
