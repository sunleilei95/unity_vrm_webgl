using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// WebGL 启动后从 URL 查询参数读取 VRM 地址并自动加载模型。
/// 支持参数名：vrm / vrmUrl / url
/// 例如：https://example.com/index.html?vrm=https%3A%2F%2Fsite.com%2Favatar.vrm
/// </summary>
public class VrmWebglUrlLoader : MonoBehaviour
{
    [Header("URL 参数名（按顺序匹配）")]
    [SerializeField] private string[] queryKeys = { "vrm", "vrmUrl", "url" };

    [Header("可选：模型父节点")]
    [SerializeField] private Transform modelRoot;

    [Header("日志")]
    [SerializeField] private bool verboseLog = true;

    [Header("下载设置")]
    [SerializeField] private int requestTimeoutSeconds = 30;

    private async void Start()
    {
        try
        {
            Log($"启动，Application.absoluteURL={Application.absoluteURL}");
            EnsureBlackBackground();

            string vrmUrl = GetVrmUrlFromAbsoluteUrl(Application.absoluteURL, queryKeys);
            if (string.IsNullOrWhiteSpace(vrmUrl))
            {
                Warn("未在 URL 查询参数中找到 VRM 地址。请通过 ?vrm=... 传入。当前完整地址见上一条日志。");
                return;
            }

            Log($"发现 VRM 地址：{vrmUrl}");
            byte[] bytes = await DownloadBytesAsync(vrmUrl);
            if (bytes == null || bytes.Length == 0)
            {
                Error("VRM 下载失败或内容为空。请检查 Network 面板中的 .vrm 请求状态码与 CORS。\n");
                return;
            }

            Log($"VRM 下载完成，字节数：{bytes.Length}");
            GameObject loaded = await LoadVrmWithUniVrmByReflection(bytes);
            if (loaded == null)
            {
                Error("未能通过 UniVRM 反射加载模型。请确认：1) 已安装 UniVRM10 运行时 2) WebGL 未裁剪掉相关类型（可加 link.xml）。");
                return;
            }

            if (modelRoot != null)
            {
                loaded.transform.SetParent(modelRoot, false);
            }

            loaded.transform.localPosition = Vector3.zero;
            loaded.transform.localRotation = Quaternion.identity;

            Log("VRM 模型加载完成。若看不到模型，请检查相机朝向、模型尺寸、光照。\n");
        }
        catch (Exception ex)
        {
            Error($"Start() 发生未处理异常：{ex}");
        }
    }

    private void EnsureBlackBackground()
    {
        if (Camera.main == null)
        {
            Warn("未找到 Camera.main（MainCamera tag）。背景颜色设置已跳过。");
            return;
        }

        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = Color.black;
    }

    private string GetVrmUrlFromAbsoluteUrl(string absoluteUrl, IReadOnlyList<string> keys)
    {
        if (string.IsNullOrEmpty(absoluteUrl))
        {
            Warn("Application.absoluteURL 为空。通常 Editor 内运行会为空，WebGL 正式页面应有值。");
            return null;
        }

        int q = absoluteUrl.IndexOf('?');
        if (q < 0 || q + 1 >= absoluteUrl.Length)
        {
            Warn("URL 不包含查询参数（? 后内容为空）。");
            return null;
        }

        string query = absoluteUrl.Substring(q + 1);
        string[] pairs = query.Split('&');

        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair))
            {
                continue;
            }

            int eq = pair.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string key = Uri.UnescapeDataString(pair.Substring(0, eq).Replace("+", "%20"));
            string value = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace("+", "%20"));
            map[key] = value;
        }

        foreach (string key in keys)
        {
            if (map.TryGetValue(key, out string found) && !string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        Warn($"查询参数存在，但不包含目标键：{string.Join(", ", keys)}。");
        return null;
    }

    private async Task<byte[]> DownloadBytesAsync(string url)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            Log($"开始下载 VRM：{url}，timeout={requestTimeoutSeconds}s");

            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            while (!op.isDone)
            {
                await Task.Yield();
            }

#if UNITY_2020_2_OR_NEWER
            bool success = req.result == UnityWebRequest.Result.Success;
#else
            bool success = !req.isNetworkError && !req.isHttpError;
#endif
            if (!success)
            {
                Error($"下载失败: error={req.error}, status={req.responseCode}, url={url}");
                return null;
            }

            Log($"下载成功: status={req.responseCode}, contentLength={req.downloadedBytes}");
            return req.downloadHandler.data;
        }
    }

    private async Task<GameObject> LoadVrmWithUniVrmByReflection(byte[] vrmBytes)
    {
        Type vrm10Type = Type.GetType("UniVRM10.Vrm10, VRM10") ?? Type.GetType("UniVRM10.Vrm10, UniVRM10");
        if (vrm10Type == null)
        {
            Error("未找到 UniVRM10.Vrm10 类型。可能是未安装 VRM10 运行时，或 WebGL 代码裁剪导致类型丢失。建议添加 Assets/link.xml。\n");
            return null;
        }

        Log($"已命中 UniVRM 类型：{vrm10Type.AssemblyQualifiedName}");

        MethodInfo loadBytesAsync = vrm10Type.GetMethod("LoadBytesAsync", BindingFlags.Public | BindingFlags.Static);
        if (loadBytesAsync == null)
        {
            Error("找到 UniVRM10.Vrm10，但未找到静态方法 LoadBytesAsync。请检查 UniVRM 版本兼容性。\n");
            return null;
        }

        try
        {
            object[] args = BuildArgsForMethod(loadBytesAsync, vrmBytes);
            object taskObj = loadBytesAsync.Invoke(null, args);
            object result = await AwaitTaskLike(taskObj);

            if (result == null)
            {
                Error("LoadBytesAsync 返回结果为空。\n");
                return null;
            }

            PropertyInfo goProp = result.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
            if (goProp == null)
            {
                Error($"LoadBytesAsync 返回类型 {result.GetType().FullName} 不包含 gameObject 属性。\n");
                return null;
            }

            return goProp.GetValue(result) as GameObject;
        }
        catch (TargetInvocationException ex)
        {
            Exception inner = ex.InnerException ?? ex;
            Error($"UniVRM 调用异常：{inner}");
            return null;
        }
        catch (Exception ex)
        {
            Error($"反射加载异常：{ex}");
            return null;
        }
    }

    private static object[] BuildArgsForMethod(MethodInfo method, byte[] vrmBytes)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object[] args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type pt = parameters[i].ParameterType;
            if (pt == typeof(byte[]))
            {
                args[i] = vrmBytes;
            }
            else if (parameters[i].HasDefaultValue)
            {
                args[i] = parameters[i].DefaultValue;
            }
            else if (pt.IsValueType)
            {
                args[i] = Activator.CreateInstance(pt);
            }
            else
            {
                args[i] = null;
            }
        }

        return args;
    }

    private static async Task<object> AwaitTaskLike(object taskObj)
    {
        if (taskObj == null)
        {
            return null;
        }

        if (taskObj is Task task)
        {
            await task;
            Type taskType = taskObj.GetType();
            if (taskType.IsGenericType)
            {
                PropertyInfo resultProp = taskType.GetProperty("Result");
                return resultProp?.GetValue(taskObj);
            }

            return null;
        }

        return null;
    }

    private void Log(string msg)
    {
        if (verboseLog)
        {
            Debug.Log($"[VrmWebglUrlLoader] {msg}");
        }
    }

    private static void Warn(string msg)
    {
        Debug.LogWarning($"[VrmWebglUrlLoader] {msg}");
    }

    private static void Error(string msg)
    {
        Debug.LogError($"[VrmWebglUrlLoader] {msg}");
    }
}
