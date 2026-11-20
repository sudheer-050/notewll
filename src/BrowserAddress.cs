using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace Notewll
{
    // Reads the address bar of the browser window being dictated into (Chrome, Edge, Brave...) through Windows UI Automation,
    // so AI sites are recognised even when the tab title is just the conversation name. Only the site name is used; nothing is stored.
    static class BrowserAddress
    {
        static readonly string[] AddressNames = { "Address and search bar", "Address bar", "Search or enter address", "Search with Google or enter address" };

        public static Task<string> ReadAsync(IntPtr window)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (window == IntPtr.Zero) return null;
                    var root = AutomationElement.FromHandle(window);
                    var edit = AddressNames.Select(name => root.FindFirst(TreeScope.Descendants, new AndCondition(
                                   new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                                   new PropertyCondition(AutomationElement.NameProperty, name)))).FirstOrDefault(e => e != null);
                    object pattern;
                    if (edit == null || !edit.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) return null;
                    string value = ((ValuePattern)pattern).Current.Value;
                    string host = PromptCrafter.HostOf(value);
                    return host; // site name only, e.g. chatgpt.com
                }
                catch { return null; }
            });
        }

        // Waits briefly for the lookup started when dictation began; never delays typing for long.
        public static string Result(Task<string> lookup, int waitMs)
        {
            try { return lookup != null && lookup.Wait(waitMs) ? lookup.Result : null; } catch { return null; }
        }
    }
}
