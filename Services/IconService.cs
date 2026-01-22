using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Rooster.Models;

namespace Rooster.Services
{
    /// <summary>
    /// Handles asynchronous icon loading with memory and disk caching.
    /// </summary>
    public class IconService : MonoBehaviour
    {
        private static IconService _instance;
        public static IconService Instance
        {
            get
            {
                if (_instance == null)
                {
                    var obj = new GameObject("IconService");
                    _instance = obj.AddComponent<IconService>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        private Dictionary<string, Sprite> _memoryCache = new Dictionary<string, Sprite>();
        private string _cachePath;

        private void Awake()
        {
            _cachePath = Path.Combine(BepInEx.Paths.PluginPath, "Rooster", "Cache", "Icons");
            if (!Directory.Exists(_cachePath))
            {
                Directory.CreateDirectory(_cachePath);
            }
        }

        public void GetIcon(ThunderstorePackage pkg, Action<Sprite> callback)
        {
            if (pkg == null || string.IsNullOrEmpty(pkg.IconUrl))
            {
                callback?.Invoke(null);
                return;
            }

            // 1. Memory Cache
            if (_memoryCache.TryGetValue(pkg.IconUrl, out var cachedSprite))
            {
                callback?.Invoke(cachedSprite);
                return;
            }

            // 2. Disk Cache
            string fileName = pkg.FullName + ".png";
            string localPath = Path.Combine(_cachePath, fileName);

            if (File.Exists(localPath))
            {
                StartCoroutine(LoadFromDisk(localPath, pkg.IconUrl, callback));
                return;
            }

            // 3. Download
            StartCoroutine(DownloadIcon(pkg.IconUrl, localPath, callback));
        }

        private IEnumerator LoadFromDisk(string localPath, string url, Action<Sprite> callback)
        {
            byte[] fileData = File.ReadAllBytes(localPath);
            Texture2D tex = new Texture2D(2, 2);
            if (ImageConversion.LoadImage(tex, fileData))
            {
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _memoryCache[url] = sprite;
                callback?.Invoke(sprite);
            }
            else
            {
                // If disk load fails, try downloading
                StartCoroutine(DownloadIcon(url, localPath, callback));
            }
            yield break;
        }

        private IEnumerator DownloadIcon(string url, string localPath, Action<Sprite> callback)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    RoosterPlugin.LogError($"Failed to download icon from {url}: {www.error}");
                    callback?.Invoke(null);
                }
                else
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    if (tex != null)
                    {
                        // Save to disk
                        try
                        {
                            byte[] pngData = ImageConversion.EncodeToPNG(tex);
                            File.WriteAllBytes(localPath, pngData);
                        }
                        catch (Exception ex)
                        {
                            RoosterPlugin.LogWarning($"Failed to save icon to disk cache: {ex.Message}");
                        }

                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        _memoryCache[url] = sprite;
                        callback?.Invoke(sprite);
                    }
                    else
                    {
                        callback?.Invoke(null);
                    }
                }
            }
        }
    }
}
