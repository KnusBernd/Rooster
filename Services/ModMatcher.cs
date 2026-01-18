using System;
using System.Collections.Generic;
using BepInEx;
using Rooster.Models;

namespace Rooster.Services
{
    /// <summary>
    /// Provides heuristic matching capabilities to associate local BepInEx plugins with Thunderstore packages.
    /// Accounts for naming discrepancies and namespace variations.
    /// </summary>
    public static class ModMatcher
    {
        public const int MIN_MATCH_SCORE = 60;

        /// <summary>Finds the best matching Thunderstore package for a plugin.</summary>
        public static ThunderstorePackage FindPackage(PluginInfo plugin, List<ThunderstorePackage> packages)
        {
            if (packages == null || packages.Count == 0) return null;

            string guid = plugin.Metadata.GUID;
            string modName = plugin.Metadata.Name;

            ThunderstorePackage bestMatch = null;
            int bestScore = 0;

            foreach (var pkg in packages)
            {
                int score = ScoreMatch(pkg, guid, modName);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = pkg;
                }
            }

            if (bestScore >= MIN_MATCH_SCORE)
            {
                RoosterPlugin.LogInfo($"Heuristic Match: {modName} ({guid}) -> {bestMatch.full_name} (Score: {bestScore})");
                return bestMatch;
            }

            return null;
        }

        /// <summary>Calculates a similarity score based on GUID, name tokens, and namespace.</summary>
        public static int ScoreMatch(ThunderstorePackage pkg, string localGuid, string localName)
        {
            int score = 0;
            
            string tsFullName = pkg.full_name ?? "";
            string[] parts = tsFullName.Split('-');
            string tsNamespace = parts.Length > 0 ? parts[0] : "";
            string tsName = parts.Length > 1 ? parts[1] : pkg.name ?? "";

            string nLocalGuid = NormalizeName(localGuid);
            string nLocalName = NormalizeName(localName);
            string nTsName = NormalizeName(tsName);
            string nTsNamespace = NormalizeName(tsNamespace);

            // Exact name match
            if (nLocalName == nTsName) score += 70; // Increased base score for exact match
            else if (nLocalName.Length > 5 && nTsName.Length > 5)
            {
                 // Fuzzy Prefix Match (e.g. "LevelLoadingOptimizer" vs "LevelLoadingOptimization")
                 int commonPrefix = GetCommonPrefixLength(nLocalName, nTsName);
                 float ratio = (float)commonPrefix / Math.Max(nLocalName.Length, nTsName.Length);
                 
                 if (ratio >= 0.75f) score += 60; // Strong similarity
                 
                 // Name containment (e.g. "CustomBlocks" in "SuperCustomBlocks")
                 else if (nTsName.Contains(nLocalName) || nLocalName.Contains(nTsName))
                 {
                     score += 50;
                 }
            }
            
            // GUID matches package name
            if (nLocalGuid == nTsName) score += 80;
            
            // GUID contains namespace and name
            if (nLocalGuid.Contains(nTsNamespace) && nLocalGuid.Contains(nTsName)) score += 100;
            else if (nLocalGuid.Contains(nTsName)) 
            {
                // Partial containment
                // Longer names provide more confidence
                if (nTsName.Length >= 12) score += 65; 
                else score += 50; 
            }

            // Token-based matching for formatting variations
            HashSet<string> localTokens = Tokenize(localName);
            HashSet<string> remoteTokens = Tokenize(tsName);
            
            if (remoteTokens.Count > 1 && localTokens.Count > remoteTokens.Count)
            {
                // Verify all remote tokens exist in local tokens
                bool allRemoteInLocal = true;
                foreach(var rt in remoteTokens)
                {
                    if (!localTokens.Contains(rt))
                    {
                        allRemoteInLocal = false;
                        break;
                    }
                }
                
                if (allRemoteInLocal) score += 65;
            }

            // Website/Repo Name Match (e.g. "CustomBlocks" vs "https://github.com/Woedroe/UCH-CustomBlocks")
            if (!string.IsNullOrEmpty(pkg.website_url))
            {
                string url = pkg.website_url.TrimEnd('/');
                int lastSlash = url.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < url.Length - 1)
                {
                    string repoName = url.Substring(lastSlash + 1);
                    string nRepoName = NormalizeName(repoName);
                    
                    if (nRepoName == nLocalName) score += 70;
                    else if (nRepoName.Contains(nLocalName) || nLocalName.Contains(nRepoName))
                    {
                         // Require sufficient length to avoid false positives with short names
                         if (nLocalName.Length > 4) 
                         {
                             // High confidence if the overlap is substantial
                             score += 70; 
                         }
                    }
                }
            }

            if (score > 40 && score < MIN_MATCH_SCORE)
            {
                RoosterPlugin.LogInfo($"Close Heuristic Miss: {localName} vs {pkg.full_name} -> Score: {score} (Repo: {pkg.website_url})");
            }

            return score;
        }

        /// <summary>Removes non-alphanumeric chars and lowercases.</summary>
        public static string NormalizeName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            char[] arr = new char[input.Length];
            int idx = 0;
            foreach(char c in input)
            {
                if (char.IsLetterOrDigit(c))
                {
                    arr[idx++] = char.ToLowerInvariant(c);
                }
            }
            return new string(arr, 0, idx);
        }

        /// <summary>Splits on CamelCase and non-alphanumeric boundaries.</summary>
        public static HashSet<string> Tokenize(string input)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(input)) return tokens;

            int start = 0;
            for (int i = 1; i < input.Length; i++)
            {
                bool isTransition = (char.IsLower(input[i-1]) && char.IsUpper(input[i])) || !char.IsLetterOrDigit(input[i]);
                
                if (isTransition)
                {
                    string sub = input.Substring(start, i - start);
                    sub = NormalizeName(sub);
                    if (sub.Length > 2) tokens.Add(sub);
                    
                    if (!char.IsLetterOrDigit(input[i])) start = i + 1;
                    else start = i;
                }
            }
            if (start < input.Length)
            {
                string sub = input.Substring(start);
                sub = NormalizeName(sub);
                if (sub.Length > 2) tokens.Add(sub);
            }
            return tokens;
        }

        public static int GetCommonPrefixLength(string s1, string s2)
        {
            int len = Math.Min(s1.Length, s2.Length);
            for (int i = 0; i < len; i++)
            {
                if (s1[i] != s2[i]) return i;
            }
            return len;
        }
    }
}
