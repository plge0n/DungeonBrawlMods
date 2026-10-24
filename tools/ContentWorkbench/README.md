# 内容包工作台

用表单做[内容包](../../docs/内容包.md)的网页工具：打开内容包文件夹，编辑 dbmod.json、技能、被动、遗物、装备特效、装备、变体英雄和翻译，
边改边按 DBModKit 的读包规则检查，保存回文件夹，或者复制到游戏、导出 zip。

## 用法

用 **Edge 或 Chrome** 双击打开 `index.html`（不用装任何东西，也不联网）。

- **新建内容包**：选一个空文件夹，填名字和 Id，会写入 `dbmod.json` 和 `content.json`。
- **打开文件夹**：选有 `dbmod.json` 的文件夹。想先看写法可以打开仓库里的 `templates/ContentPack`。
- **保存**（Ctrl+S）：只写内容改过的文件，没改的文件逐字节不动；原来用 CRLF 换行的继续用 CRLF。
  原文件里有注释或多余的逗号时会先问，因为保存后会被整理掉。JSON 写错、读不了的文件不会被覆盖。
- **复制到游戏**：选游戏文件夹（或 `BepInEx`、`BepInEx\content`），整个包会复制到 `BepInEx/content/<文件夹名>/`。
  游戏开着时到 Mod 设置 → DBModKit → 内容包里点“关闭”再点“启用”就会重新读。
- **导出 zip**：里面套一层包的文件夹，解压到 `BepInEx/content` 就能用。发到创意工坊仍然用 `pack.ps1 -Content`。

Firefox 等不支持写文件夹的浏览器只能打开查看，“保存”会改成下载 zip。

## 检查

- **错误**：`validate.js` 照搬 DBModKit 的 `ContentReader`、`ContentExprParser` 和 `ContentPacks.Load`，文字和游戏里
  （Mod 设置、trace.log）报的一样。游戏里写错的条目会整条跳过。
- **提醒**：游戏不报错、但多半是写漏了的：说明或表达式里的 `{n}` 超出数值个数、图标名 / 特效路径 / 装备编号在游戏数据里找不到、
  `MinModKit` 比用到的功能低、没写 Id、有翻译文件但漏翻了。

装备的属性名、游戏特效名按 `catalog.js` 里的游戏数据检查（和 DBModKit 注册装备时一样，不认识就整件不加载），游戏更新后要重新生成目录。
和游戏的已知差别：变体模型文件夹不存在时游戏报完整路径，这里报相对路径。

## 游戏数据（catalog.js）

下拉框里的英雄、技能、遗物、装备、装备属性名、特效路径由 `gen-catalog.py` 从游戏配置表生成。游戏更新后重新生成：

```
python gen-catalog.py --excel <导出的配置表文件夹> --decompiled <反编译源码文件夹>
```

需要 `openpyxl`。配置表和反编译源码的来源见仓库外的逆向笔记。

## 改了读包规则之后

`test/` 里是对照测试：`test/cases/*.json` 是各种写错的内容包，`test/expected.json` 是 DBModKit 的 `ContentReader` 读它们报的错。

1. 改 DBModKit 的读包代码时同步改 `validate.js`。
2. 编译 DBModKit（`-p:DeployToGame=false`），用它重新生成 `expected.json`（生成工具在仓库外的 `../tools/tests/WorkbenchParity`，
   反射调用 `ContentReader.ReadFile`）。
3. `node test/run.js`：工作台的报错要和 `expected.json` 一字不差，另外断言包级检查（重复的键、装备 / 变体的引用、Content 路径）。
