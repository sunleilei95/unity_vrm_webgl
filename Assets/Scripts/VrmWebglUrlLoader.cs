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

    private async void Start()
    {
        EnsureBlackBackground();

        string vrmUrl = GetVrmUrlFromAbsoluteUrl(Application.absoluteURL, queryKeys);
        if (string.IsNullOrWhiteSpace(vrmUrl))
        {
            Log("未在 URL 查询参数中找到 VRM 地址。请通过 ?vrm=... 传入。");
            return;
        }

        Log($"发现 VRM 地址：{vrmUrl}");
        byte[] bytes = await DownloadBytesAsync(vrmUrl);
        if (bytes == null || bytes.Length == 0)
        {
            Debug.LogError("[VrmWebglUrlLoader] VRM 下载失败或内容为空。");
            return;
        }

        GameObject loaded = await LoadVrmWithUniVrmByReflection(bytes);
        if (loaded == null)
        {
            Debug.LogError("[VrmWebglUrlLoader] 未能通过 UniVRM 反射加载模型。请确认已安装 UniVRM 运行时包。");
            return;
        }

        if (modelRoot != null)
        {
            loaded.transform.SetParent(modelRoot, false);
        }

        loaded.transform.localPosition = Vector3.zero;
        loaded.transform.localRotation = Quaternion.identity;

        Log("VRM 模型加载完成。");
    }

    private void EnsureBlackBackground()
    {
        if (Camera.main == null)
        {
            return;
        }

        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = Color.black;
    }

    private static string GetVrmUrlFromAbsoluteUrl(string absoluteUrl, IReadOnlyList<string> keys)
    {
        if (string.IsNullOrEmpty(absoluteUrl))
        {
            return null;
        }

        int q = absoluteUrl.IndexOf('?');
        if (q < 0 || q + 1 >= absoluteUrl.Length)
        {
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

            string key = Uri.UnescapeDataString(pair.Substring(0, eq));
            string value = Uri.UnescapeDataString(pair.Substring(eq + 1));
            map[key] = value;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            if (map.TryGetValue(keys[i], out string found) && !string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        return null;
    }

    private static async Task<byte[]> DownloadBytesAsync(string url)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 30;
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
                Debug.LogError($"[VrmWebglUrlLoader] 下载失败: {req.error}");
                return null;
            }

            return req.downloadHandler.data;
        }
    }

    private static async Task<GameObject> LoadVrmWithUniVrmByReflection(byte[] vrmBytes)
    {
        Type vrm10Type = Type.GetType("UniVRM10.Vrm10, VRM10");
        if (vrm10Type == null)
        {
            vrm10Type = Type.GetType("UniVRM10.Vrm10, UniVRM10");
        }

        if (vrm10Type != null)
        {
            MethodInfo loadBytesAsync = vrm10Type.GetMethod(
                "LoadBytesAsync",
                BindingFlags.Public | BindingFlags.Static
            );

            if (loadBytesAsync != null)
            {
                object[] args = BuildArgsForMethod(loadBytesAsync, vrmBytes);
                object taskObj = loadBytesAsync.Invoke(null, args);
                object result = await AwaitTaskLike(taskObj);
                if (result != null)
                {
                    PropertyInfo goProp = result.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                    if (goProp != null)
                    {
                        return goProp.GetValue(result) as GameObject;
                    }
                }
            }
        }

        Type legacyImporter = Type.GetType("VRM.VRMImporterContext, VRM");
        if (legacyImporter != null)
        {
            // 留给旧版 UniVRM 的扩展点；当前仅尝试新版本入口。
            Debug.LogWarning("[VrmWebglUrlLoader] 检测到旧版 VRM 命名空间，但未实现自动反射导入。建议升级至 UniVRM10。");
        }

        return null;
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
}
