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
        /// <summary>
        /// Downloads a file from a URL to a local destination.
        /// Optionally verifies the file hash (SHA256) if provided.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="destinationPath">The local path to save the file.</param>
        /// <param name="expectedHash">The expected SHA256 hash (optional).</param>
        /// <param name="onComplete">Callback invoked with success status and error message (if any).</param>
        public static IEnumerator DownloadFile(string url, string destinationPath, string expectedHash, Action<bool, string> onComplete)
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
                        
                        if (!string.IsNullOrEmpty(expectedHash))
                        {
                            using (var sha256 = System.Security.Cryptography.SHA256.Create())
                            {
                                byte[] hashBytes = sha256.ComputeHash(data);
                                string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                                
                                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    string error = $"Hash mismatch! Expected: {expectedHash}, Actual: {actualHash}";
                                    RoosterPlugin.LogError(error);
                                    onComplete?.Invoke(false, error);
                                    yield break;
                                }
                                RoosterPlugin.LogInfo("Hash verification passed.");
                            }
                        }

                        File.WriteAllBytes(destinationPath, data);
                        RoosterPlugin.LogInfo("Download complete and verified.");
                        onComplete?.Invoke(true, null);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Failed to save or verify file: {ex}");
                        onComplete?.Invoke(false, ex.Message);
                    }
                }
            }
        }
    }
}
