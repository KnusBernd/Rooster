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
    /// <summary> Handles icon loading with memory and disk caching. </summary>
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
        private List<string> _accessOrder = new List<string>();
        private const int MEMORY_CACHE_LIMIT = 100;
        private string _cachePath;

        private void Awake()
        {
            _cachePath = Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", "Rooster", "Icons");
            if (!Directory.Exists(_cachePath)) Directory.CreateDirectory(_cachePath);
        }

        public void GetIcon(ThunderstorePackage pkg, Action<Sprite> callback)
        {
            if (pkg == null || string.IsNullOrEmpty(pkg.IconUrl))
            {
                callback?.Invoke(null);
                return;
            }

            if (_memoryCache.TryGetValue(pkg.IconUrl, out var cachedSprite)) 
            {
                // Update access order
                _accessOrder.Remove(pkg.IconUrl);
                _accessOrder.Add(pkg.IconUrl);
                
                callback?.Invoke(cachedSprite); 
                return; 
            }

            // Prevent memory leak by capping cache size - remove oldest
            while (_memoryCache.Count >= MEMORY_CACHE_LIMIT && _accessOrder.Count > 0)
            {
                string oldestUrl = _accessOrder[0];
                _accessOrder.RemoveAt(0);
                
                if (_memoryCache.TryGetValue(oldestUrl, out var oldestSprite))
                {
                    DestroySprite(oldestSprite);
                    _memoryCache.Remove(oldestUrl);
                }
            }

            string localPath = Path.Combine(_cachePath, pkg.FullName + ".png");
            if (File.Exists(localPath))
            {
                StartCoroutine(LoadFromDisk(localPath, pkg.IconUrl, callback));
                return;
            }

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
                _accessOrder.Remove(url);
                _accessOrder.Add(url);

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
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                // Create a texture download handler with 'readable' set to true
                // This is required for ImageConversion.EncodeToPNG to work
                var handler = new DownloadHandlerTexture(true);
                www.downloadHandler = handler;

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    RoosterPlugin.LogWarning($"[IconService] Failed to download icon: {www.error} ({url})");
                    callback?.Invoke(null);
                }
                else
                {
                    Texture2D tex = handler.texture;
                    if (tex != null)
                    {
                        try
                        {
                            byte[] pngData = ImageConversion.EncodeToPNG(tex);
                            if (pngData != null)
                            {
                                File.WriteAllBytes(localPath, pngData);
                                // RoosterPlugin.LogInfo($"[IconService] Cached icon to disk: {Path.GetFileName(localPath)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            RoosterPlugin.LogWarning($"[IconService] Failed to cache icon: {ex.Message}");
                        }

                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        
                        _memoryCache[url] = sprite;
                        _accessOrder.Remove(url);
                        _accessOrder.Add(url);

                        callback?.Invoke(sprite);
                    }
                    else
                    {
                        callback?.Invoke(null);
                    }
                }
            }
        }
        public void ClearMemoryCache()
        {
            foreach (var sprite in _memoryCache.Values)
            {
                DestroySprite(sprite);
            }
            _memoryCache.Clear();
            _accessOrder.Clear();
            RoosterPlugin.LogInfo("[IconService] Memory cache cleared.");
        }

        private void DestroySprite(Sprite sprite)
        {
            if (sprite != null && sprite.texture != null)
            {
                Destroy(sprite.texture);
            }
            if (sprite != null)
            {
                Destroy(sprite);
            }
        }
    }
}
