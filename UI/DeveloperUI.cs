using System;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using Rooster.Models;
using Rooster.Services;
using UnityEngine;

namespace Rooster.UI
{
    public class DeveloperUI : MonoBehaviour
    {
        private static bool _showWindow = false;
        private Rect _windowRect = new Rect(20, 20, 600, 500);
        private int _selectedTab = 0;
        private string[] _tabs = { "Live Inspector", "Match Simulator" };

        // Inspector State
        private PluginInfo _selectedPlugin;
        private Vector2 _inspectorScroll;

        // Simulator State
        private string _simGuid = "";
        private string _simName = "";
        private string _simTsPackage = "";
        private string _simUrl = "";
        private MatchReport _simReport;
        
        // Simulator Picker State
        private bool _showLocalMods = false;
        private Vector2 _simulatorPluginScroll;

        // Placeholders
        private const string PLACEHOLDER_GUID = "com.author.modname";
        private const string PLACEHOLDER_NAME = "ModName";
        private const string PLACEHOLDER_PKG = "Author-ModName";
        private const string PLACEHOLDER_URL = "https://github.com/Author/ModName";

        public static void Toggle()
        {
            _showWindow = !_showWindow;
        }

        private void Update()
        {
            if (RoosterConfig.DeveloperKey != null && Input.GetKeyDown(RoosterConfig.DeveloperKey.Value))
            {
                Toggle();
            }
        }

        private void OnGUI()
        {
            if (!_showWindow) return;

            _windowRect = GUI.Window(999, _windowRect, DrawWindow, "Rooster Developer Tools");
        }

        private void DrawWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            GUILayout.BeginHorizontal();
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (GUILayout.Toggle(_selectedTab == i, _tabs[i], "Button"))
                {
                    _selectedTab = i;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (_selectedTab == 0) DrawInspector();
            else DrawSimulator();
        }

        private void DrawInspector()
        {
            GUILayout.BeginHorizontal();
            
            // Left Panel: Plugin List
            GUILayout.BeginVertical(GUILayout.Width(250));
            _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll, "Box");
            
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                string displayName = plugin.Metadata.Name;
                if (UpdateChecker.MatchedPackages.ContainsKey(plugin.Metadata.GUID))
                {
                    displayName = "[MATCHED] " + displayName;
                }

                if (GUILayout.Button(displayName))
                {
                    _selectedPlugin = plugin;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Right Panel: Details
            GUILayout.BeginVertical("Box");
            if (_selectedPlugin != null)
            {
                GUILayout.Label($"<b>Selected: {_selectedPlugin.Metadata.Name}</b>");
                GUILayout.Label($"GUID: {_selectedPlugin.Metadata.GUID}");
                GUILayout.Label($"Version: {_selectedPlugin.Metadata.Version}");
                
                GUILayout.Space(10);
                GUILayout.Label("<b>Match Analysis:</b>");

                if (UpdateChecker.MatchedPackages.TryGetValue(_selectedPlugin.Metadata.GUID, out var pkg))
                {
                    GUILayout.Label($"Matched To: <color=green>{pkg.full_name}</color>");
                    
                    // Re-run matching to get the breakdown live
                    MatchReport report = ModMatcher.ScoreMatch(pkg, _selectedPlugin.Metadata.GUID, _selectedPlugin.Metadata.Name);
                    DrawReport(report);
                }
                else
                {
                    GUILayout.Label("Status: <color=red>Not Matched</color>");
                    GUILayout.Label("Top Candidates:");
                    
                    // Scan for best candidates to explain why they failed
                    if (UpdateChecker.CachedPackages != null) 
                    {
                        var candidates = UpdateChecker.CachedPackages
                            .Select(p => new { Pkg = p, Report = ModMatcher.ScoreMatch(p, _selectedPlugin.Metadata.GUID, _selectedPlugin.Metadata.Name) })
                            .OrderByDescending(x => x.Report.TotalScore)
                            .Take(3);

                        foreach (var c in candidates)
                        {
                            GUILayout.Label($"Candidate: {c.Pkg.full_name} (Score: {c.Report.TotalScore})");
                            if (GUILayout.Button("View Breakdown"))
                            {
                                DrawReport(c.Report);
                            }
                        }
                    }
                }
            }
            else
            {
                GUILayout.Label("Select a plugin to inspect.");
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawSimulator()
        {
            GUILayout.Label("<b>Local Mod Details (Simulation)</b>");
            
            if (GUILayout.Button(_showLocalMods ? "Hide Mod List" : "Pick Installed Mod..."))
            {
                _showLocalMods = !_showLocalMods;
            }

            if (_showLocalMods)
            {
                GUILayout.BeginVertical("Box", GUILayout.Height(150));
                _simulatorPluginScroll = GUILayout.BeginScrollView(_simulatorPluginScroll);
                foreach (var plugin in Chainloader.PluginInfos.Values)
                {
                    if (GUILayout.Button($"{plugin.Metadata.Name} ({plugin.Metadata.GUID})"))
                    {
                        _simName = plugin.Metadata.Name;
                        _simGuid = plugin.Metadata.GUID;
                        _showLocalMods = false;
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }

            float labelWidth = 160f;

            GUILayout.BeginHorizontal();
            GUILayout.Label("GUID:", GUILayout.Width(labelWidth));
            TextFieldWithPlaceholder(ref _simGuid, PLACEHOLDER_GUID);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(labelWidth));
            TextFieldWithPlaceholder(ref _simName, PLACEHOLDER_NAME);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("<b>Target Thunderstore Package</b>");
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Package Name:", GUILayout.Width(labelWidth));
            TextFieldWithPlaceholder(ref _simTsPackage, PLACEHOLDER_PKG);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Repo URL (Optional):", GUILayout.Width(labelWidth));
            TextFieldWithPlaceholder(ref _simUrl, PLACEHOLDER_URL);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Simulate Match"))
            {
                // Use placeholders if empty
                string useGuid = string.IsNullOrEmpty(_simGuid) ? PLACEHOLDER_GUID : _simGuid;
                string useName = string.IsNullOrEmpty(_simName) ? PLACEHOLDER_NAME : _simName;
                string usePkg = string.IsNullOrEmpty(_simTsPackage) ? PLACEHOLDER_PKG : _simTsPackage;
                string useUrl = string.IsNullOrEmpty(_simUrl) ? PLACEHOLDER_URL : _simUrl;

                var mockPkg = new ThunderstorePackage 
                { 
                    full_name = usePkg, 
                    name = usePkg.Contains("-") ? usePkg.Split('-')[1] : usePkg,
                    website_url = useUrl 
                };
                _simReport = ModMatcher.ScoreMatch(mockPkg, useGuid, useName);
            }

            GUILayout.Space(10);

            if (_simReport != null)
            {
                GUILayout.Label("<b>Simulation Results:</b>");
                DrawReport(_simReport);
            }
        }

        private void DrawReport(MatchReport report)
        {
            Color scoreColor = report.TotalScore >= ModMatcher.MIN_MATCH_SCORE ? Color.green : Color.red;
            GUI.color = scoreColor;
            GUILayout.Label($"Total Score: {report.TotalScore} / {ModMatcher.MIN_MATCH_SCORE}");
            GUI.color = Color.white;

            foreach (var line in report.Breakdown)
            {
                GUILayout.Label(line);
            }
        }

        private void TextFieldWithPlaceholder(ref string text, string placeholder)
        {
            text = GUILayout.TextField(text);
            if (string.IsNullOrEmpty(text))
            {
                var lastRect = GUILayoutUtility.GetLastRect();
                if (lastRect.width > 0)
                {
                    GUI.color = new Color(1, 1, 1, 0.5f);
                    GUI.Label(lastRect, " " + placeholder);
                    GUI.color = Color.white;
                }
            }
        }
    }
}
