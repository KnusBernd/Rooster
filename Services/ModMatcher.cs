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
        public const int AMBIGUITY_THRESHOLD = 5; // If two packages are within 5 points, reject both as ambiguous.

        /// <summary>Finds the best matching Thunderstore package for a plugin.</summary>
        public static ThunderstorePackage FindPackage(PluginInfo plugin, List<ThunderstorePackage> packages)
        {
            if (packages == null || packages.Count == 0) return null;

            string guid = plugin.Metadata.GUID;
            string modName = plugin.Metadata.Name;

            ThunderstorePackage bestMatch = null;
            int bestScore = 0;
            bool isAmbiguous = false;

            foreach (var pkg in packages)
            {
                MatchReport report = ScoreMatch(pkg, guid, modName);
                if (report.TotalScore > bestScore)
                {
                    // If the new score is significantly better, it's not ambiguous anymore
                    if (report.TotalScore >= bestScore + AMBIGUITY_THRESHOLD)
                    {
                        isAmbiguous = false;
                    }
                    else
                    {
                        isAmbiguous = true;
                    }

                    bestScore = report.TotalScore;
                    bestMatch = pkg;
                }
                else if (report.TotalScore > 0 && Math.Abs(report.TotalScore - bestScore) < AMBIGUITY_THRESHOLD)
                {
                    // If we find another package with a very similar score, mark as ambiguous
                    isAmbiguous = true;
                }
            }

            if (isAmbiguous && bestScore >= MIN_MATCH_SCORE)
            {
                RoosterPlugin.LogWarning($"Ambiguous Match for {modName}: Best score {bestScore} but another package is too similar. Skipping auto-match.");
                return null;
            }

            if (bestMatch != null && bestScore >= MIN_MATCH_SCORE)
            {
                RoosterPlugin.LogInfo($"Heuristic Match: {modName} ({guid}) -> {bestMatch.FullName} (Score: {bestScore})");
                return bestMatch;
            }

            return null;
        }

        /// <summary>Calculates a similarity score based on GUID, name tokens, and namespace.</summary>
        public static MatchReport ScoreMatch(ThunderstorePackage pkg, string localGuid, string localName)
        {
            MatchReport report = new MatchReport();

            string tsFullName = pkg.FullName ?? "";
            string[] parts = tsFullName.Split('-');
            string tsNamespace = parts.Length > 0 ? parts[0] : "";
            string tsName = parts.Length > 1 ? parts[1] : pkg.Name ?? "";

            string nLocalGuid = NormalizeName(localGuid);
            string nLocalName = NormalizeName(localName);
            string nTsName = NormalizeName(tsName);
            string nTsNamespace = NormalizeName(tsNamespace);

            if (nLocalName == nTsName)
            {
                report.AddScore("Exact Plugin Name Match", 70);
            }
            else if (nLocalName.Length > 5 && nTsName.Length > 5)
            {
                int commonPrefix = GetCommonPrefixLength(nLocalName, nTsName);
                float ratio = (float)commonPrefix / Math.Max(nLocalName.Length, nTsName.Length);

                if (ratio >= 0.75f)
                {
                    report.AddScore($"Fuzzy Match (Prefix Ratio {ratio:F2})", 60);
                }
                else if (nTsName.Contains(nLocalName) || nLocalName.Contains(nTsName))
                {
                    report.AddScore("Name Containment", 50);
                }
            }

            if (nLocalGuid == nTsName)
            {
                report.AddScore("GUID matches Package Name", 80);
            }

            if (nLocalGuid.Contains(nTsNamespace) && nLocalGuid.Contains(nTsName))
            {
                report.AddScore("GUID contains Author + ModName", 100);
            }
            else if (nLocalGuid.Contains(nTsName))
            {
                if (nTsName.Length >= 12)
                    report.AddScore("GUID contains Long Package Name", 65);
                else
                    report.AddScore("GUID contains Short Package Name", 50);
            }

            // If the local GUID contains a dot or hyphen, it likely uses a namespace (e.g. Author.ModName)
            if (localGuid.Contains(".") || localGuid.Contains("-"))
            {
                if (!nLocalGuid.Contains(nTsNamespace))
                {
                    // If the GUID has a namespace but it doesn't match the package author, penalize
                    report.AddScore("Namespace mismatch penalty", -100);
                }
            }

            HashSet<string> localTokens = Tokenize(localName);
            HashSet<string> remoteTokens = Tokenize(tsName);

            if (localTokens.Count > 0 && remoteTokens.Count > 0)
            {
                int shared = 0;
                foreach (var rt in remoteTokens)
                {
                    if (localTokens.Contains(rt)) shared++;
                }

                float overlap = (float)shared / Math.Max(localTokens.Count, remoteTokens.Count);

                bool safeMatch = shared >= 2 || (shared == 1 && Math.Max(localTokens.Count, remoteTokens.Count) <= 2);

                if (safeMatch)
                {
                    if (overlap >= 0.8f)
                    {
                        report.AddScore($"High Token Overlap ({overlap:P0})", 75);
                    }
                    else if (overlap >= 0.65f)
                    {
                        report.AddScore($"Good Token Overlap ({overlap:P0})", 65);
                    }
                    else if (overlap >= 0.5f)
                    {
                        report.AddScore($"Moderate Token Overlap ({overlap:P0})", 55);
                    }
                }
            }

            if (!string.IsNullOrEmpty(pkg.WebsiteUrl))
            {
                string url = pkg.WebsiteUrl.TrimEnd('/');
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
                RoosterPlugin.LogInfo($"Close Heuristic Miss: {localName} vs {pkg.FullName} -> Score: {report.TotalScore}");
            }

            return report;
        }

        /// <summary>Removes non-alphanumeric chars and lowercases.</summary>
        public static string NormalizeName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            char[] arr = new char[input.Length];
            int idx = 0;
            foreach (char c in input)
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
                bool isTransition = (char.IsLower(input[i - 1]) && char.IsUpper(input[i])) || !char.IsLetterOrDigit(input[i]);

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
