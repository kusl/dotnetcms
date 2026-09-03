using Microsoft.Playwright;

namespace MyBlog.E2E;

/// <summary>
/// Playwright helpers for driving a Blazor Server page deterministically.
/// </summary>
/// <remarks>
/// Every page arrives twice: first as prerendered static HTML, then again from the
/// interactive circuit once the SignalR connection is established. During the gap
/// between the two, <c>blazor.web.js</c> still owns anchor clicks and services them
/// with enhanced navigation — <c>history.pushState</c> plus a background fetch.
/// <para>
/// A click that lands inside that gap pushes the new URL straight away, so a
/// <c>WaitForURLAsync</c> succeeds, but the circuit was started for the *previous*
/// URL. When its first render batch arrives it rebuilds the DOM for the page the test
/// just navigated away from, and nothing reconciles the two afterwards: the address
/// bar shows the new route while the markup shows the old one, permanently. That is a
/// silent, timing-dependent failure that only shows up on a loaded CI runner.
/// </para>
/// <para>
/// Waiting for interactivity closes the gap. It also protects form input: attaching
/// the circuit replaces the prerendered DOM, discarding anything already typed.
/// </para>
/// </remarks>
public static class BlazorPageExtensions
{
    /// <summary>
    /// Marker rendered by <c>MainLayout</c>, whose value flips to <c>true</c> only when
    /// <c>RendererInfo.IsInteractive</c> is true — that is, when the circuit, and not the
    /// static prerenderer, produced the markup currently in the DOM.
    /// </summary>
    private const string InteractiveMarkerSelector = ".layout[data-blazor-interactive='true']";

    /// <summary>
    /// Budget for the circuit to connect. Deliberately generous because a cold CI
    /// runner can be slow; in practice this wait is satisfied in well under a second.
    /// </summary>
    private const float DefaultTimeoutMs = 30000;

    /// <summary>
    /// Waits until the Blazor Server circuit has connected and rendered the current page.
    /// Call this before clicking links or filling forms so the interaction is handled by
    /// the interactive router rather than racing Blazor's startup.
    /// </summary>
    /// <param name="page">The page under test.</param>
    /// <param name="timeout">Maximum time to wait, in milliseconds.</param>
    public static async Task WaitForBlazorInteractiveAsync(this IPage page, float timeout = DefaultTimeoutMs)
    {
        var marker = page.Locator(InteractiveMarkerSelector);
        await Assertions.Expect(marker).ToBeAttachedAsync(new() { Timeout = timeout });
    }
}
