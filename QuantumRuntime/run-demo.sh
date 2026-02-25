#!/bin/bash
# 演示脚本：运行 Phase 2B & 2C

cd "d:\AI\量子经典兼容系统\QuantumRuntime"

echo "编译项目..."
dotnet build

echo ""
echo "运行 Phase 1 演示..."
dotnet run --project QuantumRuntime.csproj
