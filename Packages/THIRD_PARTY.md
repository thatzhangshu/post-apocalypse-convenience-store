# 微信 SDK 来源

- 包：`com.qq.weixin.minigame-0.1.18.tgz`
- 官方仓库：https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk
- 固定提交：`981890fdaa8b7e3419c37ef9a68264d835bcd7de`
- package.json 版本：`0.1.18`
- 包文件 SHA-256：`D273CA1140BDF28B08D240299B46D84F1D956DE80ECFCF776398885C7DEDC156`
- 选择依据：本机团结 `2022.3.55t3` 的 `WeixinMiniGameSupport/wxsdkver.txt` 标注 `v0.1.18`。
- 包由该提交的原始受版本控制文件生成，未修改 SDK 源码，保留包内 LICENSE 文件。
- 本地 tarball 是为了让工程导入不依赖包管理器的 GitHub 网络连通性；不代表已通过真机兼容验证。

重新生成方法（在官方 SDK 克隆目录中）：

```sh
git archive --format=tar.gz --prefix=package/ --output=com.qq.weixin.minigame-0.1.18.tgz 981890fdaa8b7e3419c37ef9a68264d835bcd7de
```

引擎与 SDK 升级应一起验证，不直接替换成浮动的 main 分支。
