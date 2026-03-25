# Unity WebGL VRM URL 自动加载

这个仓库提供一个 Unity 脚本：WebGL 运行后，从页面 URL 参数读取 VRM 文件地址，自动下载并加载模型，并将相机背景设为纯黑。

## 脚本
- `Assets/Scripts/VrmWebglUrlLoader.cs`

## 使用方式
1. 在 Unity 项目中安装 UniVRM（建议 UniVRM10 运行时）。
2. 把 `VrmWebglUrlLoader` 挂到场景中的任意 GameObject。
3. （可选）给 `Model Root` 指定一个父节点，用于放置加载后的模型。
4. Build Settings 切到 **WebGL** 并导出。
5. 访问导出页面时带上参数：
   - `?vrm=https://your-cdn.com/avatar.vrm`
   - 或 `?vrmUrl=...`
   - 或 `?url=...`

## 示例 URL
```text
https://your-site.com/index.html?vrm=https%3A%2F%2Fexample.com%2Fgirl.vrm
```

## 说明
- 脚本会读取 `Application.absoluteURL` 中的查询参数。
- 背景会在运行时设置为纯黑（`Camera.main.backgroundColor = Color.black`）。
- 若未安装 UniVRM 或接口不匹配，脚本会输出错误日志。
