# 内存卫士网站部署

版本 0.6.2，目标：https://www.xiaopuwa.com/memory-guardian/

解压 MemoryGuardian-0.6.2-website.zip，将 memory-guardian 文件夹整体上传到网站根目录。确保为 memory-guardian/index.html，不要套两层目录。

目录仅包含网页、assets 资源及 downloads 内的最新版安装包。源码、免安装版、许可证和校验值通过 GitHub 提供：https://github.com/syq10086/MemoryGuardian

上传后检查安装包和 GitHub 入口。建议服务器将旧 /earth-guardian/ 地址 301 跳转至 /memory-guardian/，兼容旧版更新入口。新版软件已使用新地址。

后续更新：运行 scripts/build-installer.ps1，移除 downloads 旧安装包，再运行 scripts/package-site.ps1。保留 GitHub 历史标签作为对应源码获取途径。
