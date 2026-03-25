# Unity WebGL VRM URL 自动加载（黑色背景）

这个仓库提供一个可直接挂载的 Unity 脚本：
- WebGL 启动后，从页面 URL 参数读取 VRM 资源地址；
- 自动下载并加载 VRM 模型；
- 将背景设置为纯黑（`#000000`）。

脚本文件：`Assets/Scripts/VrmWebglUrlLoader.cs`

---

## 1. 功能说明

`VrmWebglUrlLoader` 会在 `Start()` 时完成以下流程：
1. 设置 `Camera.main` 为纯黑背景；
2. 读取 `Application.absoluteURL`；
3. 解析查询参数（按顺序匹配 `vrm` / `vrmUrl` / `url`）；
4. 下载 `.vrm` 文件（二进制）；
5. 通过 UniVRM 运行时入口加载模型并实例化到场景。

---

## 2. 安装环境（建议）

> 以下是**推荐环境**，可避免大量版本兼容问题。

- **Unity**：`2022.3 LTS`（推荐）
- **Build Target**：WebGL
- **Scripting Backend**：IL2CPP（WebGL 默认）
- **API Compatibility Level**：`.NET Standard 2.1`（默认通常即可）
- **UniVRM**：建议使用支持 Unity 2022.3 LTS 的版本（见下方下载方式）

UniVRM 官方仓库的安装说明显示：当前版本支持 Unity 2022.3 LTS 或更高版本（以官方 release 页面为准）。

---

## 3. UniVRM 下载与安装（详细）

你可以用两种方式安装 UniVRM：

## A) 方式一：UnityPackage（最直观）

1. 打开 UniVRM Releases：
   - https://github.com/vrm-c/UniVRM/releases
2. 进入最新版本（或你指定的稳定版本）。
3. 在 Assets 中下载对应包：
   - `VRM-*.unitypackage`（VRM 1.0）
   - `UniVRM-*.unitypackage`（VRM 0.x）
4. 将 `.unitypackage` 拖到 Unity Editor 中导入。

> 新项目优先使用 VRM 1.0 相关包；如果要兼容旧资源，再补 VRM 0.x。

## B) 方式二：UPM Git URL（便于团队锁版本）

Unity 中打开：`Window -> Package Manager -> + -> Add package from git URL...`

常见包：
- `com.vrmc.gltf`
- `com.vrmc.vrm`（VRM 1.0）
- `com.vrmc.univrm`（VRM 0.x）

示例（将 `v0.131.0` 改成你想锁定的版本号）：

```text
https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.0
https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.0
https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM#v0.131.0
```

> 注：新版本 UniVRM 的包路径已迁移到 `/Packages/...`；老版本示例常见 `/Assets/...` 路径，使用时请看对应 release 的安装说明。

---

## 4. 在场景中接入脚本

1. 创建一个空物体（例如 `VrmLoader`）。
2. 挂载 `VrmWebglUrlLoader`。
3. 可选：创建一个空节点 `ModelRoot`，拖到脚本的 `Model Root` 字段。
4. 运行后模型会挂在 `ModelRoot` 下（若未指定则直接放在场景根）。

---

## 5. WebGL 导出设置

1. `File -> Build Settings -> WebGL -> Switch Platform`
2. `Player Settings` 中建议：
   - Color Space：Linear（按项目需求）
   - Compression Format：Gzip/Brotli（按部署环境支持）
3. 点击 `Build` 导出。

---

## 6. URL 参数用法

支持参数名：
- `vrm`（优先）
- `vrmUrl`
- `url`

示例：

```text
https://your-site.com/index.html?vrm=https%3A%2F%2Fcdn.example.com%2Favatar.vrm
```

也可：

```text
https://your-site.com/index.html?vrmUrl=https%3A%2F%2Fcdn.example.com%2Favatar.vrm
```

建议对 VRM 链接做 URL 编码（`encodeURIComponent`）。

---

## 7. 常见问题（非常重要）

## 1) 模型下载失败 / 控制台报 CORS
请确保 VRM 文件服务器返回允许跨域：
- `Access-Control-Allow-Origin: *`（或你的站点域名）

WebGL 页面和 VRM 资源常常不在同一域名，CORS 不正确就会加载失败。

## 2) HTTPS 页面加载 HTTP 资源失败
如果你的页面是 `https://`，VRM 地址也必须尽量使用 `https://`，避免 mixed content 被浏览器拦截。

## 3) UniVRM 未安装或版本不匹配
脚本会输出错误日志。请先确认：
- 已导入 UniVRM 运行时；
- 版本与 Unity 版本兼容；
- 如果使用 UPM，Tag 与路径配置正确。

## 4) 背景不是黑色
脚本只会设置 `Camera.main`。请确认主相机有 `MainCamera` 标签，且场景里确实存在主相机。

---

## 8. 快速自测清单

- [ ] 场景有主相机（`MainCamera` tag）
- [ ] 已安装 UniVRM（建议 2022.3 对应版本）
- [ ] `VrmWebglUrlLoader` 已挂载
- [ ] 访问 URL 包含 `?vrm=...`
- [ ] VRM 文件地址可公网访问
- [ ] 资源服务器已配置 CORS

---

## 9. 官方参考链接

- UniVRM GitHub 仓库：
  - https://github.com/vrm-c/UniVRM
- UniVRM Releases（下载 unitypackage / 查看 UPM URL）：
  - https://github.com/vrm-c/UniVRM/releases
- VRM 官方文档（英文）：
  - https://vrm.dev/en/univrm/
