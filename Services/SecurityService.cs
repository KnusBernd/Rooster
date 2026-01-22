using System;
using System.Collections.Generic;

namespace Rooster.Services
{
    public static class SecurityService
    {
        private static readonly HashSet<string> TrustedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "thunderstore.io",
            "github.com",
            "raw.githubusercontent.com",
            "github-releases.githubusercontent.com"
        };

        public static bool IsTrustedDomain(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                var uri = new Uri(url);
                string host = uri.Host.ToLowerInvariant();

                if (TrustedDomains.Contains(host)) return true;

                // Handle subdomains for GitHub
                if (host.EndsWith(".github.com") || host.EndsWith(".githubusercontent.com"))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        public static bool IsValidWebUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
