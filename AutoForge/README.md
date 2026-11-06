# AutoForge · 自动锻体

版本 0.40.0，依赖 DBModKit 0.40.0。白名单 Mod，可以打榜；联机只处理本地玩家。没有实机测试，只验到主菜单。

默认开启，F10 切换，Ctrl+F10 留给 RunLog；Mod 菜单 → AutoForge → 设置可改开关、模式、热键与全部属性权重（0–10），也可恢复默认权重。配置在 BepInEx/config/AutoForge.json，重启保留。不自动购买锻造器，关闭或候选分数全为 0 时手选。

权重模式：Σ 权重 × 候选数值 / 基准 maxValue，双属性相加。基准优先 D 档；原表有 19 种属性没有 D/C 档，采用最低实际档 B 的 maxValue（下表注明）。百分比沿用游戏/表的百分点单位。Three 为独立属性，不额外拆三项。

均衡模式：从本次候选出现且权重大于 0 的属性中，先补本局累计归一化收益 / 权重最少的项；相同则比较权重分数，再相同选靠前项。本局累计包含安装期间的手选与自动选择，新局清零，游戏快照成功时保存/还原。热重载/局中首次安装无法恢复此前收益；快照补丁不可用时无法跟随回档。更改权重不清空累计。

成功自动选择后显示 2.5 秒提示，连续选择合并计数；详细候选、档次、得分、下标见 RunLog。异常保留原生界面并写日志。原生确认回调内部异常由游戏记录，不能撤销已应用收益。

| 属性 type | 游戏名（表内） | 默认权重 | 基准 maxValue | 基准档 |
|---|---|---:|---:|---|
| AddDamage | 伤害 | 5 | 4 | B |
| Agi | 敏捷 | 5 | 4 | D |
| AllBreakShield | 全能破盾 | 2 | 4 | D |
| ArmedAdd | 武装 | 5 | 8 | B |
| Armor | 护甲 | 3 | 4 | D |
| Attack | 攻击力 | 5 | 12 | D |
| AttackSpeed | 攻击速度 | 5 | 6 | D |
| CoolDown | 冷却 | 5 | 5 | B |
| Critical | 暴击 | 5 | 5 | D |
| CriticalDamage | 暴击伤害 | 5 | 12 | B |
| Doge | 闪避 | 3 | 5 | B |
| ExpAdd | 经验 | 1 | 4 | B |
| ExtraDamage | 附加伤害 | 5 | 50 | B |
| HP | 生命 | 3 | 120 | D |
| HPSec | 生命回复 | 3 | 20 | D |
| HaloAdd | 刻印 | 5 | 8 | B |
| HenshinAdd | 变身 | 5 | 8 | B |
| HpPercent | 大生命 | 3 | 5 | B |
| HpSecRate | 大恢复 | 3 | 1 | B |
| Luck | 幸运值 | 1 | 15 | D |
| MP | 法力 | 1 | 20 | D |
| MPSec | 法力回复 | 1 | 2 | D |
| MoveSpeed | 移速 | 2 | 0.5 | B |
| NormalAdd | 物理伤害 | 5 | 6 | B |
| NormalBreak | 物理破盾 | 2 | 6 | D |
| ReduceInjury | 固定减伤 | 3 | 18 | B |
| SkillAdd | 法术伤害 | 5 | 6 | B |
| SkillBreak | 法术破盾 | 2 | 6 | D |
| SkillHit | 魔抗 | 3 | 5 | B |
| Sta | 耐力 | 5 | 4 | D |
| Str | 力量 | 5 | 4 | D |
| SummonAdd | 召唤师 | 5 | 8 | B |
| Three | 全能 | 5 | 3 | D |
| XiXue | 攻击恢复 | 3 | 15 | B |
| XiXueRate | 生命偷取 | 3 | 1.5 | B |
