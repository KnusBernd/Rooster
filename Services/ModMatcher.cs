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
                MatchReport report = ScoreMatch(pkg, guid, modName);
                if (report.TotalScore > bestScore)
                {
                    bestScore = report.TotalScore;
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
        public static MatchReport ScoreMatch(ThunderstorePackage pkg, string localGuid, string localName)
        {
            MatchReport report = new MatchReport();
            
            string tsFullName = pkg.full_name ?? "";
            string[] parts = tsFullName.Split('-');
            string tsNamespace = parts.Length > 0 ? parts[0] : "";
            string tsName = parts.Length > 1 ? parts[1] : pkg.name ?? "";

            string nLocalGuid = NormalizeName(localGuid);
            string nLocalName = NormalizeName(localName);
            string nTsName = NormalizeName(tsName);
            string nTsNamespace = NormalizeName(tsNamespace);

            // Exact name match
            if (nLocalName == nTsName) 
            {
                report.AddScore("Exact Plugin Name Match", 70);
            }
            else if (nLocalName.Length > 5 && nTsName.Length > 5)
            {
                 // Fuzzy Prefix Match (e.g. "LevelLoadingOptimizer" vs "LevelLoadingOptimization")
                 int commonPrefix = GetCommonPrefixLength(nLocalName, nTsName);
                 float ratio = (float)commonPrefix / Math.Max(nLocalName.Length, nTsName.Length);
                 
                 if (ratio >= 0.75f) 
                 {
                    report.AddScore($"Fuzzy Match (Prefix Ratio {ratio:F2})", 60);
                 }
                 
                 // Name containment (e.g. "CustomBlocks" in "SuperCustomBlocks")
                 else if (nTsName.Contains(nLocalName) || nLocalName.Contains(nTsName))
                 {
                     report.AddScore("Name Containment", 50);
                 }
            }
            
            // GUID matches package name
            if (nLocalGuid == nTsName) 
            {
                report.AddScore("GUID matches Package Name", 80);
            }
            
            // GUID contains namespace and name
            if (nLocalGuid.Contains(nTsNamespace) && nLocalGuid.Contains(nTsName)) 
            {
                report.AddScore("GUID contains Author + ModName", 100);
            }
            else if (nLocalGuid.Contains(nTsName)) 
            {
                // Partial containment
                // Longer names provide more confidence
                if (nTsName.Length >= 12) 
                    report.AddScore("GUID contains Long Package Name", 65);
                else 
                    report.AddScore("GUID contains Short Package Name", 50); 
            }

            // Token-based matching for formatting variations
            HashSet<string> localTokens = Tokenize(localName);
            HashSet<string> remoteTokens = Tokenize(tsName);
            
            if (localTokens.Count > 0 && remoteTokens.Count > 0)
            {
                int shared = 0;
                foreach(var rt in remoteTokens)
                {
                    if (localTokens.Contains(rt)) shared++;
                }

                float overlap = (float)shared / Math.Max(localTokens.Count, remoteTokens.Count);
                
                if (overlap >= 0.8f) // High overlap (e.g. UCHTeams vs TeamsUCH)
                {
                    report.AddScore($"High Token Overlap ({overlap:P0})", 75);
                }
                else if (overlap >= 0.5f) // Moderate overlap
                {
                     // Requires at least 2 tokens to match to be safe?
                     if (shared >= 2 || (shared == 1 && Math.Max(localTokens.Count, remoteTokens.Count) <= 2))
                     {
                        report.AddScore($"Moderate Token Overlap ({overlap:P0})", 55);
                     }
                }
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
                    
                     if (nRepoName == nLocalName) 
                    {
                        report.AddScore("URL Repo Name matches Plugin Name", 70);
                    }
                    else if (nRepoName.Contains(nLocalName) || nLocalName.Contains(nRepoName))
                    {
                         // Require sufficient length to avoid false positives with short names
                         if (nLocalName.Length > 4) 
                         {
                             report.AddScore("URL Repo Name overlaps Plugin Name", 70);
                         }
                         else
                         {
                             report.AddScore("URL Repo Name overlap ignored (Short Name)", 0);
                         }
                    }
                    else
                    {
                         report.AddScore($"URL Repo Name mismatch ('{repoName}' vs Local)", 0);
                    }
                }
            }

            if (report.TotalScore > 40 && report.TotalScore < MIN_MATCH_SCORE)
            {
                RoosterPlugin.LogInfo($"Close Heuristic Miss: {localName} vs {pkg.full_name} -> Score: {report.TotalScore}");
            }

            return report;
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
