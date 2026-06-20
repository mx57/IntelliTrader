#!/bin/bash
mkdir -p Publish/bin
dotnet publish IntelliTrader/IntelliTrader.csproj -f netcoreapp2.1 -c Release -o "Publish/bin"
dotnet publish IntelliTrader.Web/IntelliTrader.Web.csproj -f netcoreapp2.1 -c Release -o "Publish/bin"
