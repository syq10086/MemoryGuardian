# 内存卫士下载页部署

目标地址：`https://www.xiaopuwa.com/earth-guardian/`

纯静态页面，不需要数据库、PHP、Node 或前端依赖。图片、样式、脚本和下载文件均在同一目录，没有第三方字体、统计脚本或 CDN 依赖。

## 上传步骤

1. 解压 `MemoryGuardian-0.6.0-website.zip`。
2. 将 **earth-guardian 文件夹整体**上传到网站面板中 `www.xiaopuwa.com` 对应的网站根目录，不是服务器 `/` 根目录。
3. 确认路径为 `网站根目录/earth-guardian/index.html`，同级有 `assets/` 和 `downloads/`，不要套两层 earth-guardian。
4. 访问上述地址，检查安装包、免安装版和对应源码三个下载入口。
5. 软件右键菜单和“关于”的更新入口都指向此地址；网站上传前该页面可能不存在。

可与 PHP/FastAdmin 网站共存。如果现有重写规则接管全部路径，请让已经存在的静态文件和目录直接访问。默认首页需包含 `index.html`。若服务器拦截 EXE，只对本下载目录允许该扩展名；推荐 `Content-Type: application/octet-stream`、`Content-Disposition: attachment`。不要关闭整体安全防护。

本次只制作本地上传包，没有连接或修改线上服务器。页面未虚构备案号，正式上线时可按主站要求添加主站已有的真实备案信息。

## 内容和素材

- 软件名称：内存卫士；版本 0.6.0；开发者、公众号、抖音：宋域强。
- URL 目录保留 earth-guardian；内部 EXE 保留 EarthGuardian.exe，兼容现有安装与启动任务。
- DDR5 价格是作者个人看到的报价，不是整体市场价格结论。
- `assets/developer-poster.jpg` 是用户原照片的本地合成排版，保留面貌，不是 AI 生成照片。原照片在 `assets/song-yuqiang.jpg`；重建使用 `scripts/create-site-assets.ps1`。
- 网页交互不读取或清理访客内存，颜色展示为近似视觉示意。软件使用 WPF 三维材质。
- 下载目录包括同版完整源码、GPL 许可证、修改与上游声明以及 SHA-256 校验值。
- 未使用数字签名，不保证消除 SmartScreen 提醒，不要求用户关闭安全防护。

## 后续打包

1. 更新项目版本与网页版本、日期、下载文件名。
2. 运行 `scripts/build-installer.ps1`、`scripts/package.ps1`，最后运行 `scripts/package-site.ps1`。
3. 源码 ZIP 排除网站 downloads 目录，避免把安装包或源码 ZIP 再次嵌入源码。
4. 上传新的网站目录；为旧版接收者保留对应源码的获取途径，避免安装包与源码版本不对应。
