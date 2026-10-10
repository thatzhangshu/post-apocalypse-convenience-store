# 微信小游戏链路验证

本测试用于验证团结编辑器、微信小游戏构建、配置资源、触摸、音频、存档和前后台恢复。测试场景不包含经营玩法。

## 环境检查

- 团结编辑器：`2022.3.55t3 (60da82e5d6ab)`，工程记录的团结版本为 `1.5.2`。
- 编辑器程序：`C:/Program Files/Tuanjie/Hub/Editor/2022.3.55t3/Editor/Tuanjie.exe`。
- WebGL 和 WeixinMiniGame 构建模块已安装。
- 已实际通过批处理启动、授权检查、创建项目、微信平台切换与测试脚本编译。
- 微信小游戏导出已通过，日志 `Logs/export-wechat.log` 记录成功并退出码 0；导出目录约 9.8 MiB（包含调试文件，不等于平台计费或审核包体口径）。
- 微信 SDK：`0.1.18`，固定源码提交及许可证见 `Packages/THIRD_PARTY.md`。
- SDK 使用工程内固定 tarball；源码包约 22 MB，不是最终小游戏包体。
- 微信开发者工具：在已检查的安装登记、常见目录和快捷方式中尚未发现，需确认或安装。
- AppID 和开发者权限：尚待提供及确认。
- 手机运行、听感和退出恢复：尚未验证。

## 打开场景

1. 在团结 Hub 中添加仓库根目录，使用上述完整版本打开。
2. 首次导入后，选择 `Game > Platform Test > 1 - Create or open test scene`。
3. 打开 Game 视图，选择竖屏比例，点击 Play。
4. 检查中文显示、旋转方块和按钮。

场景路径：`Assets/_Game/PlatformSmokeTest/Scenes/PlatformSmokeTest.unity`。

菜单首次创建场景；后续执行会打开已有场景，不重建覆盖。若想保留当前其他场景的未保存修改，按编辑器提示先保存。

## 测试配置

权威配置为 `Configs/SmokeTest.json`。修改后执行 `Game > Platform Test > 2 - Sync configuration`，再运行场景。

`Assets/_Game/PlatformSmokeTest/Data/SmokeTest.json` 是游戏侧副本，不手工编辑。导出前会同步并校验。该测试 JSON 不读取或改写既有的 `Configs/config.xls`。

## 本地批处理

先关闭当前工程的编辑器窗口，避免同一工程被两个编辑器占用。在仓库根目录执行：

```powershell
./Tools/Run-PlatformTest.ps1 -Action Setup
./Tools/Run-PlatformTest.ps1 -Action Validate
./Tools/Run-PlatformTest.ps1 -Action ExportWeChat
```

无 AppID 时仅用于验证导出，不能据此认定开发者工具或真机链路通过。已有 AppID 时使用：

```powershell
./Tools/Run-PlatformTest.ps1 -Action ExportWeChat -AppId '你的真实小游戏AppID'
```

也可以在编辑器中使用 `Game > Platform Test > 4 - Export WeChat test`。自定义导出命令从当前进程环境变量 `WX_APPID` 读取 AppID；没有提供时保持空值。使用 SDK 自带转换窗口时按该窗口要求填写。

导出目录为 `Builds/WeChatSmoke/`，开发者工具应导入其中 `minigame/` 子目录。测试使用数据分包，首次选择不使用 CDN。若实际包体超过当前平台限制，再调整资源分包或托管方案。

## 开发者工具与真机验收

1. 从微信官方文档下载并安装稳定版开发者工具：https://developers.weixin.qq.com/miniprogram/dev/devtools/download.html
2. 登录拥有该小游戏开发权限的微信账号。
3. 导入 `Builds/WeChatSmoke/minigame/`，填写真实小游戏 AppID。
4. 编译并查看控制台，检查 `[SmokeTest]` 日志。
5. 运行预览或真机调试，使用被授权的微信账号在手机上打开。
6. 逐项填写下面的实际结果。

| 项目 | 操作 | 通过标准 | 当前结果 |
| --- | --- | --- | --- |
| 构建 | 执行 ExportWeChat | 退出码 0 且导出 game.js、game.json、project.config.json | 已通过，AppID 留空 |
| SDK | 进入手机测试页面 | 出现 WX SDK ready 日志，无初始化失败 | 待测 |
| 渲染与中文 | 观察标题、配置消息和方块 | 中文无方框，方块持续旋转 | 待测 |
| 触摸 | 连续点击 Tap +1 | 每次触摸仅增加一次 | 待测 |
| 音频 | 点击 Play sound | 手机能够听到提示音 | 待测 |
| 即时存取 | 加到 3、Save、加到 4、Load | 恢复为 3 | 待测 |
| 持久存档 | Save 后完全关闭小游戏，再重新打开 | 自动读取保存数值 | 待测 |
| 前后台 | 切换其他 App 再回来 | Show/Hide 更新，输入恢复 | 待测 |
| 重置 | 点击 Reset test save，再重启 | 测试计数为 0 | 待测 |
| 安全区 | 在有刘海或底部手势条的手机观察 | 文字与按钮不被遮挡 | 待测 |

编辑器使用 PlayerPrefs，微信运行时使用 WX 同步存储接口。二者数据不共享；编辑器成功不能代替手机持久化测试。测试只操作 `convenience-store.smoke.v1.count` 键。

SDK 导出文件目前声明基础库 `3.5.1` 与 UnityPlugin `1.2.57`。这是固定 SDK 的导出结果，尚未在开发者工具确认当前可用性。如果导入时报插件版本或授权错误，记录原始提示后再调整平台配置。

## 字体与音频来源

`SmokeSansSC.ttf` 是 Noto Sans SC 的重命名字体子集，只包含当前测试所需中文及 ASCII 字符；保留 SIL Open Font License 于同目录 `OFL.txt`。新增中文后应重新生成子集，校验工具会报告缺字。

源字体：https://github.com/google/fonts/tree/main/ofl/notosanssc

音频由脚本生成，是短正弦波提示音。已提交生成资源，普通运行与构建无需 Python。

重新生成资源时，需 Python、`fonttools==4.60.1`，并下载官方源字体和许可证至 `Logs/NotoSansSC.ttf`、`Logs/NotoSansSC-OFL.txt`，然后运行 `Tools/Generate-SmokeAssets.py`。

## 记录纪律

日志位于忽略目录 `Logs/`，可能包含编辑器授权诊断，不提交或完整公开。SDK 导出的 `Builds/` 也不进入 Git。记录设备型号、微信版本、构建提交、测试时间和实际结果，保留失败信息。
