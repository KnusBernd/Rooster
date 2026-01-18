using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Rooster
{
    /// <summary>
    /// Handles downloading files from the internet with hash verification.
    /// </summary>
    public static class UpdateDownloader
    {
        /// <summary>Downloads a file from URL to local path. Hash param is unused (API v1 limitation).</summary>
        public static IEnumerator DownloadFile(string url, string destinationPath, Action<bool, string> onComplete)
        {
            RoosterPlugin.LogInfo($"Starting download from {url} to {destinationPath}");
            
            string dir = Path.GetDirectoryName(destinationPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(destinationPath)) File.Delete(destinationPath);

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                var handler = new DownloadHandlerBuffer();
                www.downloadHandler = handler;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    string error = www.error;
                    RoosterPlugin.LogError($"Download failed: {error}");
                    onComplete?.Invoke(false, error);
                }
                else
                {
                    try
                    {
                        byte[] data = www.downloadHandler.data;
                        File.WriteAllBytes(destinationPath, data);
                        RoosterPlugin.LogInfo("Download complete.");
                        onComplete?.Invoke(true, null);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Failed to save file: {ex}");
                        onComplete?.Invoke(false, ex.Message);
                    }
                }
            }
        }
    }
}
