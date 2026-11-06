# 打榜资格离线检查

运行：`dotnet run --project tools/tests/Ranked/Ranked.Tests.csproj -c Release`

由项目公共工具目录 `tools/ranked-validation/checks` 的原有 38 项检查整理而来，直接链接生产 `Ranked.cs`，使用桩隔离 Unity、Steam 和插件，不启动游戏、不访问存档、不部署到游戏目录。

覆盖新大厅格式 `2|0或1|Base64原因`、旧格式和缺字段保守拦截、五秒检查间隔、大厅及内容版本变化和战斗开始立即重算、日志按结果去重，以及上传、快照和公开接口仍完整检查并记录。BepInEx 原装补丁豁免由公共工具目录的 `tests/RankedBepInEx` 独立验证。
