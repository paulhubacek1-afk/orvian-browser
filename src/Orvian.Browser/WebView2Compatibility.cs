using Microsoft.Web.WebView2.Core;
using System.Threading.Tasks;

namespace Orvian.Browser;

internal static class WebView2Compatibility
{
    // Compatibility helper for the browser shell's existing environment readiness check.
    internal static Task<CoreWebView2Environment?> CreateCoreWebView2ControllerOptionsAsync(this CoreWebView2Environment environment)
        => Task.FromResult<CoreWebView2Environment?>(environment);
}
