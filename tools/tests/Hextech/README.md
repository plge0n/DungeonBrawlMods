# 海克斯回合离线检查

在仓库根目录运行 `dotnet run --project tools/tests/Hextech/Hextech.Tests.csproj -c Release`。

测试直接编译生产代码 `Hextech/RoundPlan.cs`，覆盖回合次数、目标波次、品质和漏回合补弹。重试检查模拟了快照还原后的计数和已安排波次。测试无需启动游戏，也无需访问存档或部署插件。
