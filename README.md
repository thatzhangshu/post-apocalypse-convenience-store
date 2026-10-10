# 末日便利店

微信小游戏原型项目。玩家经营便利店，从顾客与 NPC 获取情报，前往城市节点探索，并将行动结果带回经营与剧情。

## 当前状态

当前仓库包含团结工程、微信链路测试场景，以及三日玩法原型。原型串联固定视角便利店、NPC 对话、情报、节点探索、按时段营业、日结与本地存档。微信开发者工具与真机验收仍待完成。

玩法原型入口和配置说明见 [三日原型使用指南](Docs/Prototype.md)。编辑器菜单 `Game > Prototype > 1 - Create or open prototype` 可打开原型，随后点击 Play。

打开与验收步骤见 [微信小游戏链路验证](Docs/WechatSmokeTest.md)。

近期目标：

1. 完成程序与策划之间的配置和代码协作。
2. 用最小测试场景验证微信小游戏构建与真机运行。
3. 试玩三日原型，验证经营选择、营业与探索取舍，以及探索结果对下一次经营的影响，再扩展完整 Demo。

## 引擎与环境

- 本次验证使用团结引擎 `2022.3.55t3`（团结 1.5.2），已通过批处理启动与授权检查。
- 引擎版本以 `ProjectSettings/ProjectVersion.txt` 为准；完整链路通过后再确认正式开发基线。
- 全员使用同一编辑器完整版本及项目包依赖。
- 微信 SDK `0.1.18` 已固定为本地包，来源见 `Packages/THIRD_PARTY.md`。

## 获取项目

```sh
git clone https://github.com/thatzhangshu/post-apocalypse-convenience-store.git
cd post-apocalypse-convenience-store
```

已有本地工作目录的成员继续使用原目录，无须再次初始化或克隆到其中。

工程位于仓库根目录，`Assets`、`Packages`、`ProjectSettings` 与本文件同级。通过团结 Hub 添加根目录，打开测试场景后运行。

## 协作入口

详细步骤见 [协作指南](CONTRIBUTING.md)。

- `main`：可供双方获取的稳定版本。
- 程序：短期功能分支开发，验证后合并到 `main`。
- 策划：小规模配置修改可以直接提交 `main`；配置结构变化使用分支并与程序协调。
- 已有链路测试配置 `Configs/SmokeTest.json` 与校验菜单；正式玩法配置系统尚未建立。

## 文件管理

- 提交 `Assets` 及对应 `.meta`、`Packages`、`ProjectSettings`、配置与项目文档。
- 忽略生成缓存、个人编辑器设置、日志和构建输出。
- Unity 资源使用 Visible Meta Files 和 Force Text 设置。
- 导出包统一放入根目录下的 `Builds/`、`Exports/` 或 `minigame/`。
- Git LFS 暂未启用；大型美术源文件进入仓库前再共同确定使用方式。
- 账号密码、访问令牌和 AppSecret 不进入仓库。

## 首次协作验收

- 策划接受仓库邀请并成功克隆。
- 策划提交一项双方约定的文档修改，程序拉取确认。
- 程序合并一项修改，策划拉取确认。
- 配置系统完成后，用一次真实对话配置修改重复这套往返流程。
