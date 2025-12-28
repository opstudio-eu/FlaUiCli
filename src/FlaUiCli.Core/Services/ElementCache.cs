using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FlaUI.Core.AutomationElements;

namespace FlaUiCli.Core.Services;

// Note: This cache is for session-local element ID generation only.
// IDs are not used for any security-sensitive purposes.

public class ElementCache
{
    private readonly ConcurrentDictionary<string, WeakReference<AutomationElement>> _cache = new();
    private readonly ConcurrentDictionary<AutomationElement, string> _reverseCache = new();

    public string GetOrCreateId(AutomationElement element)
    {
        if (_reverseCache.TryGetValue(element, out var existingId))
        {
            return existingId;
        }

        var id = GenerateId(element);
        
        // Ensure uniqueness
        var baseId = id;
        var counter = 1;
        while (_cache.ContainsKey(id))
        {
            id = $"{baseId}_{counter++}";
        }

        _cache[id] = new WeakReference<AutomationElement>(element);
        _reverseCache[element] = id;
        
        return id;
    }

    public AutomationElement? GetElement(string id)
    {
        if (_cache.TryGetValue(id, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var element))
            {
                try
                {
                    // Verify element is still valid
                    _ = element.IsEnabled;
                    return element;
                }
                catch
                {
                    // Element no longer valid
                    _cache.TryRemove(id, out _);
                }
            }
            else
            {
                _cache.TryRemove(id, out _);
            }
        }
        return null;
    }

    public void Clear()
    {
        _cache.Clear();
        _reverseCache.Clear();
    }

    private static string GenerateId(AutomationElement element)
    {
        var sb = new StringBuilder();
        
        // Build a unique identifier based on element properties
        sb.Append(element.ControlType.ToString());
        
        if (!string.IsNullOrEmpty(element.AutomationId))
        {
            sb.Append('_');
            sb.Append(SanitizeForId(element.AutomationId));
        }
        else if (!string.IsNullOrEmpty(element.Name))
        {
            sb.Append('_');
            sb.Append(SanitizeForId(element.Name));
        }
        
        // Add a short hash to ensure uniqueness
        var runtimeId = element.Properties.RuntimeId.ValueOrDefault;
        if (runtimeId != null && runtimeId.Length > 0)
        {
            sb.Append('_');
            sb.Append(GetShortHash(string.Join(".", runtimeId)));
        }
        
        return sb.ToString().ToLowerInvariant();
    }

    private static string SanitizeForId(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        var result = new StringBuilder();
        foreach (var c in input.Take(20))
        {
            if (char.IsLetterOrDigit(c))
                result.Append(c);
            else if (c == ' ' || c == '_' || c == '-')
                result.Append('_');
        }
        return result.ToString();
    }

    private static string GetShortHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        // Use SHA256 instead of MD5 (even though this is just for ID generation, not security)
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }
}
