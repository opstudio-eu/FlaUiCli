using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using FlaUiCli.Core.Models;

namespace FlaUiCli.Core.Services;

public class AutomationService : IDisposable
{
    private UIA3Automation? _automation;
    private FlaUI.Core.Application? _attachedApp;
    private readonly ElementCache _elementCache = new();

    public bool IsConnected => _attachedApp != null && !_attachedApp.HasExited;
    public int? AttachedProcessId => _attachedApp?.ProcessId;
    public string? AttachedProcessName => _attachedApp?.Name;

    public void EnsureAutomation()
    {
        _automation ??= new UIA3Automation();
    }

    public List<ProcessInfo> ListProcesses()
    {
        var result = new List<ProcessInfo>();
        var processes = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .OrderBy(p => p.ProcessName);

        foreach (var proc in processes)
        {
            try
            {
                result.Add(new ProcessInfo
                {
                    Pid = proc.Id,
                    Name = proc.ProcessName,
                    MainWindowTitle = proc.MainWindowTitle,
                    HasWindow = proc.MainWindowHandle != IntPtr.Zero
                });
            }
            catch
            {
                // Skip processes we can't access
            }
        }

        return result;
    }

    public void Connect(int processId)
    {
        EnsureAutomation();
        var process = Process.GetProcessById(processId);
        _attachedApp = FlaUI.Core.Application.Attach(process);
        _elementCache.Clear();
    }

    public void Connect(string processName)
    {
        EnsureAutomation();
        var processes = Process.GetProcessesByName(processName.Replace(".exe", ""));
        if (processes.Length == 0)
            throw new InvalidOperationException($"Process '{processName}' not found");
        
        _attachedApp = FlaUI.Core.Application.Attach(processes[0]);
        _elementCache.Clear();
    }

    public void Disconnect()
    {
        _attachedApp = null;
        _elementCache.Clear();
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to any application. Use 'connect' first.");
    }

    public List<WindowInfo> GetWindows()
    {
        EnsureConnected();
        EnsureAutomation();

        var result = new List<WindowInfo>();
        var windows = _attachedApp!.GetAllTopLevelWindows(_automation!);

        foreach (var window in windows)
        {
            var id = _elementCache.GetOrCreateId(window);
            result.Add(new WindowInfo
            {
                Id = id,
                Title = window.Title ?? string.Empty,
                AutomationId = window.AutomationId ?? string.Empty,
                ClassName = window.ClassName ?? string.Empty,
                Bounds = ToBoundsInfo(window.BoundingRectangle),
                IsModal = window.IsModal,
                ProcessId = window.Properties.ProcessId.ValueOrDefault
            });
        }

        return result;
    }

    public WindowInfo? GetMainWindow()
    {
        EnsureConnected();
        EnsureAutomation();

        var window = _attachedApp!.GetMainWindow(_automation!);
        if (window == null) return null;

        var id = _elementCache.GetOrCreateId(window);
        return new WindowInfo
        {
            Id = id,
            Title = window.Title ?? string.Empty,
            AutomationId = window.AutomationId ?? string.Empty,
            ClassName = window.ClassName ?? string.Empty,
            Bounds = ToBoundsInfo(window.BoundingRectangle),
            IsModal = window.IsModal,
            ProcessId = window.Properties.ProcessId.ValueOrDefault
        };
    }

    public void FocusWindow(string? windowId = null)
    {
        EnsureConnected();
        EnsureAutomation();

        Window window;
        if (string.IsNullOrEmpty(windowId))
        {
            window = _attachedApp!.GetMainWindow(_automation!);
        }
        else
        {
            var element = _elementCache.GetElement(windowId);
            window = element?.AsWindow() ?? throw new InvalidOperationException($"Window '{windowId}' not found");
        }

        window.Focus();
    }

    public List<ElementInfo> GetElementTree(int depth = 5, string? rootId = null)
    {
        EnsureConnected();
        EnsureAutomation();

        AutomationElement root;
        if (string.IsNullOrEmpty(rootId))
        {
            root = _attachedApp!.GetMainWindow(_automation!);
        }
        else
        {
            root = _elementCache.GetElement(rootId) 
                ?? throw new InvalidOperationException($"Element '{rootId}' not found");
        }

        return new List<ElementInfo> { BuildElementInfo(root, depth) };
    }

    private ElementInfo BuildElementInfo(AutomationElement element, int depth)
    {
        var id = _elementCache.GetOrCreateId(element);
        var info = new ElementInfo { Id = id };
        
        try { info.AutomationId = element.Properties.AutomationId.ValueOrDefault ?? string.Empty; } catch { info.AutomationId = string.Empty; }
        try { info.Name = element.Properties.Name.ValueOrDefault ?? string.Empty; } catch { info.Name = string.Empty; }
        try { info.ControlType = element.Properties.ControlType.ValueOrDefault.ToString(); } catch { info.ControlType = "Unknown"; }
        try { info.ClassName = element.Properties.ClassName.ValueOrDefault ?? string.Empty; } catch { info.ClassName = string.Empty; }
        try { info.Bounds = ToBoundsInfo(element.BoundingRectangle); } catch { }
        try { info.IsEnabled = element.Properties.IsEnabled.ValueOrDefault; } catch { }
        try { info.IsOffscreen = element.Properties.IsOffscreen.ValueOrDefault; } catch { }
        try { info.HasKeyboardFocus = element.Properties.HasKeyboardFocus.ValueOrDefault; } catch { }
        try { info.Patterns = element.GetSupportedPatterns().Select(p => p.Name).ToList(); } catch { info.Patterns = new List<string>(); }

        // Get text/value if available
        try
        {
            if (element.Patterns.Value.IsSupported)
            {
                info.Value = element.Patterns.Value.Pattern.Value.Value;
            }
        }
        catch { }

        try
        {
            if (element.Patterns.Text.IsSupported)
            {
                info.Text = element.Patterns.Text.Pattern.DocumentRange.GetText(-1);
            }
        }
        catch { }

        if (depth > 0)
        {
            var children = element.FindAllChildren();
            if (children.Length > 0)
            {
                info.Children = children.Select(c => BuildElementInfo(c, depth - 1)).ToList();
            }
        }

        return info;
    }

    public List<ElementInfo> FindElements(QueryCriteria criteria)
    {
        EnsureConnected();
        EnsureAutomation();

        AutomationElement root;
        if (!string.IsNullOrEmpty(criteria.ParentId))
        {
            root = _elementCache.GetElement(criteria.ParentId)
                ?? throw new InvalidOperationException($"Parent element '{criteria.ParentId}' not found");
        }
        else
        {
            root = _attachedApp!.GetMainWindow(_automation!);
        }

        var conditions = new List<ConditionBase>();
        var cf = _automation!.ConditionFactory;

        if (!string.IsNullOrEmpty(criteria.AutomationId))
            conditions.Add(cf.ByAutomationId(criteria.AutomationId));

        if (!string.IsNullOrEmpty(criteria.Name))
            conditions.Add(cf.ByName(criteria.Name));

        if (!string.IsNullOrEmpty(criteria.ControlType) && 
            Enum.TryParse<ControlType>(criteria.ControlType, true, out var ct))
            conditions.Add(cf.ByControlType(ct));

        if (!string.IsNullOrEmpty(criteria.ClassName))
            conditions.Add(cf.ByClassName(criteria.ClassName));

        ConditionBase condition = conditions.Count switch
        {
            0 => cf.ByControlType(ControlType.Custom).Not(), // Match all
            1 => conditions[0],
            _ => new AndCondition(conditions.ToArray())
        };

        AutomationElement[] elements;
        try 
        {
            elements = criteria.FirstOnly
                ? new[] { root.FindFirstDescendant(condition) }.Where(e => e != null).ToArray()!
                : root.FindAllDescendants(condition);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to find elements: {ex.Message}", ex);
        }

        return elements
            .Where(e => e != null)
            .Select(e => BuildElementInfo(e!, 0))
            .ToList();
    }

    public ElementInfo? GetElement(string elementId)
    {
        var element = _elementCache.GetElement(elementId);
        if (element == null) return null;
        return BuildElementInfo(element, 0);
    }

    public void Click(string elementId, bool rightClick = false)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (rightClick)
            element.RightClick();
        else
            element.Click();
    }

    public void DoubleClick(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");
        
        element.DoubleClick();
    }

    public void TypeText(string elementId, string text)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        element.Focus();
        
        if (element.Patterns.Value.IsSupported)
        {
            element.Patterns.Value.Pattern.SetValue(text);
        }
        else
        {
            Keyboard.Type(text);
        }
    }

    public void ClearText(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.Value.IsSupported)
        {
            element.Patterns.Value.Pattern.SetValue(string.Empty);
        }
        else
        {
            element.Focus();
            Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
            Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
        }
    }

    public void PressKey(string elementId, string key)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        element.Focus();
        
        if (Enum.TryParse<FlaUI.Core.WindowsAPI.VirtualKeyShort>(key.ToUpperInvariant(), out var vk))
        {
            Keyboard.Type(vk);
        }
        else
        {
            throw new ArgumentException($"Unknown key: {key}");
        }
    }

    public void SetCheckState(string elementId, bool check)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.Toggle.IsSupported)
        {
            var current = element.Patterns.Toggle.Pattern.ToggleState.Value;
            var target = check ? ToggleState.On : ToggleState.Off;
            
            if (current != target)
            {
                element.Patterns.Toggle.Pattern.Toggle();
            }
        }
        else
        {
            throw new InvalidOperationException("Element does not support toggle pattern");
        }
    }

    public void Toggle(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.Toggle.IsSupported)
        {
            element.Patterns.Toggle.Pattern.Toggle();
        }
        else
        {
            element.Click();
        }
    }

    public void Select(string elementId, string itemText)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.Selection.IsSupported)
        {
            // Find item by text and select
            var item = element.FindFirstDescendant(e => e.ByName(itemText));
            if (item != null && item.Patterns.SelectionItem.IsSupported)
            {
                item.Patterns.SelectionItem.Pattern.Select();
                return;
            }
        }

        if (element.AsComboBox() != null)
        {
            var comboBox = element.AsComboBox();
            comboBox.Expand();
            Wait.UntilResponsive(comboBox, TimeSpan.FromSeconds(1));
            var item = comboBox.Items.FirstOrDefault(i => i.Text == itemText);
            item?.Select();
            return;
        }

        throw new InvalidOperationException($"Cannot select '{itemText}' in element");
    }

    public void Expand(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.ExpandCollapse.IsSupported)
        {
            element.Patterns.ExpandCollapse.Pattern.Expand();
        }
        else
        {
            throw new InvalidOperationException("Element does not support expand/collapse pattern");
        }
    }

    public void Collapse(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.ExpandCollapse.IsSupported)
        {
            element.Patterns.ExpandCollapse.Pattern.Collapse();
        }
        else
        {
            throw new InvalidOperationException("Element does not support expand/collapse pattern");
        }
    }

    public void Invoke(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            element.Click();
        }
    }

    public string? GetText(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.Text.IsSupported)
        {
            return element.Patterns.Text.Pattern.DocumentRange.GetText(-1);
        }

        if (element.Patterns.Value.IsSupported)
        {
            return element.Patterns.Value.Pattern.Value.Value;
        }

        return element.Name;
    }

    public string? GetValue(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        if (element.Patterns.Value.IsSupported)
        {
            return element.Patterns.Value.Pattern.Value.Value;
        }

        if (element.Patterns.RangeValue.IsSupported)
        {
            return element.Patterns.RangeValue.Pattern.Value.Value.ToString();
        }

        return null;
    }

    public Dictionary<string, object> GetState(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        var state = new Dictionary<string, object>
        {
            ["isEnabled"] = element.IsEnabled,
            ["isOffscreen"] = element.IsOffscreen,
            ["hasKeyboardFocus"] = element.Properties.HasKeyboardFocus.ValueOrDefault
        };

        if (element.Patterns.Toggle.IsSupported)
        {
            state["toggleState"] = element.Patterns.Toggle.Pattern.ToggleState.Value.ToString();
        }

        if (element.Patterns.SelectionItem.IsSupported)
        {
            state["isSelected"] = element.Patterns.SelectionItem.Pattern.IsSelected.Value;
        }

        if (element.Patterns.ExpandCollapse.IsSupported)
        {
            state["expandCollapseState"] = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value.ToString();
        }

        return state;
    }

    public List<string> GetPatterns(string elementId)
    {
        var element = _elementCache.GetElement(elementId)
            ?? throw new InvalidOperationException($"Element '{elementId}' not found");

        return element.GetSupportedPatterns().Select(p => p.Name).ToList();
    }

    public ElementInfo? WaitForElement(QueryCriteria criteria, int timeoutMs = 5000)
    {
        EnsureConnected();
        EnsureAutomation();

        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        
        while (DateTime.Now < deadline)
        {
            var elements = FindElements(criteria);
            if (elements.Count > 0)
                return elements[0];
            
            Thread.Sleep(100);
        }

        return null;
    }

    public bool WaitForElementGone(string elementId, int timeoutMs = 5000)
    {
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        
        while (DateTime.Now < deadline)
        {
            var element = _elementCache.GetElement(elementId);
            if (element == null || element.IsOffscreen)
                return true;
            
            try
            {
                // Try to access a property - if element is gone, this will fail
                _ = element.IsEnabled;
            }
            catch
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    public bool WaitForEnabled(string elementId, int timeoutMs = 5000)
    {
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        
        while (DateTime.Now < deadline)
        {
            var element = _elementCache.GetElement(elementId);
            if (element?.IsEnabled == true)
                return true;
            
            Thread.Sleep(100);
        }

        return false;
    }

    public string TakeScreenshot(string? elementId = null, string? outputPath = null)
    {
        EnsureConnected();
        EnsureAutomation();

        Rectangle bounds;
        if (!string.IsNullOrEmpty(elementId))
        {
            var element = _elementCache.GetElement(elementId)
                ?? throw new InvalidOperationException($"Element '{elementId}' not found");
            var rect = element.BoundingRectangle;
            bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }
        else
        {
            var window = _attachedApp!.GetMainWindow(_automation!);
            var rect = window.BoundingRectangle;
            bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

        outputPath ??= Path.Combine(Path.GetTempPath(), $"flaui_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        bitmap.Save(outputPath, ImageFormat.Png);

        return outputPath;
    }

    public string TakeScreenshotBase64(string? elementId = null)
    {
        EnsureConnected();
        EnsureAutomation();

        Rectangle bounds;
        if (!string.IsNullOrEmpty(elementId))
        {
            var element = _elementCache.GetElement(elementId)
                ?? throw new InvalidOperationException($"Element '{elementId}' not found");
            var rect = element.BoundingRectangle;
            bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }
        else
        {
            var window = _attachedApp!.GetMainWindow(_automation!);
            var rect = window.BoundingRectangle;
            bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static BoundsInfo? ToBoundsInfo(System.Drawing.Rectangle rect)
    {
        if (rect.IsEmpty) return null;
        return new BoundsInfo
        {
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height
        };
    }

    public void Dispose()
    {
        _automation?.Dispose();
        _elementCache.Clear();
    }
}
