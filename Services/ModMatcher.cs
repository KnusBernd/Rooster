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
        /// <summary>
        /// The minimum score required to consider a match valid.
        /// </summary>
        public const int MIN_MATCH_SCORE = 60;

        /// <summary>
        /// Attempts to find the best matching Thunderstore package for a given plugin.
        /// </summary>
        /// <param name="plugin">The local plugin info.</param>
        /// <param name="packages">The list of available Thunderstore packages.</param>
        /// <returns>The best matching package, or null if no match exceeds the threshold.</returns>
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

        /// <summary>
        /// Calculates a similarity score between a local plugin and a remote package.
        /// Considers GUID, name tokens, and namespace overlaps.
        /// </summary>
        /// <param name="pkg">The remote Thunderstore package.</param>
        /// <param name="localGuid">The local plugin GUID.</param>
        /// <param name="localName">The local plugin name.</param>
        /// <returns>An integer score representing the match confidence.</returns>
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
            if (nLocalName == nTsName) score += 60;
            
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

            return score;
        }

        
        
        
        /// <summary>
        /// Normalizes a string by removing non-alphanumeric characters and converting to lowercase.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The normalized string.</returns>
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

        /// <summary>
        /// Splits a string into unique normalized tokens based on CamelCase and non-alphanumeric boundaries.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>A hash set of unique tokens.</returns>
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
    }
}
