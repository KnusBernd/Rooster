using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using BepInEx.Logging;

namespace Rooster.Services
{
    /// <summary>
    /// Centralized helper for network operations using UnityWebRequest.
    /// Handles headers, error checking, and basic buffering.
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Performs a GET request with optional headers and retry logic.
        /// </summary>
        /// <param name="url">Target URL</param>
        /// <param name="headers">Optional dictionary of request headers</param>
        /// <param name="onComplete">Callback with (success, text/error)</param>
        /// <param name="retries">Number of retries on network error</param>
        public static IEnumerator Get(string url, Dictionary<string, string> headers, Action<bool, string> onComplete, int retries = 0)
        {
            for (int i = 0; i <= retries; i++)
            {
                using (UnityWebRequest www = UnityWebRequest.Get(url))
                {
                    if (headers != null)
                    {
                        foreach (var kvp in headers)
                        {
                            www.SetRequestHeader(kvp.Key, kvp.Value);
                        }
                    }

                    // Default user agent if not provided
                    if (headers == null || !headers.ContainsKey("User-Agent"))
                    {
                        www.SetRequestHeader("User-Agent", "RoosterModManager");
                    }

                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                    {
                        string errorMsg = www.error;
                        long code = www.responseCode;
                        
                        // Pass specific HTTP errors immediately without retry if they are client errors (4xx), except maybe 429
                        if (code >= 400 && code < 500 && code != 429)
                        {
                            onComplete?.Invoke(false, $"{code}: {errorMsg}");
                            yield break; 
                        }

                        if (i == retries)
                        {
                            onComplete?.Invoke(false, $"{code}: {errorMsg}");
                            yield break;
                        }
                        
                        // Wait before retry
                        yield return new UnityEngine.WaitForSecondsRealtime(1.0f);
                    }
                    else
                    {
                        onComplete?.Invoke(true, www.downloadHandler.text);
                        yield break;
                    }
                }
            }
        }
    }
}
