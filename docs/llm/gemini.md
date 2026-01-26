tests are failing 
I have included the latest dump in `dump.txt`
please read each and every line of the code 
don't skim it 
don't try to change the coding style 
don't remove braces around the if 
don't change the primary constructor back to whatever you did 
don't make unnecessary changes 
do make the code build, 
do make the tests pass 
and do make everything work properly 
and follow engineering best practices 
and please do not hallucinate 
give me full files for all files that changed 
[INFO] Waiting for MyBlog to be ready...
.
[INFO] MyBlog is ready!
[INFO] Running E2E tests...
b20251967cd4ed0d0953fedf5aeccca2bec11c41549ea1e845d2cc8107f73bd4
[myblog-web] | cannot open `/run/user/1000/crun/a20111a1b34d1757f96fa10fbec4a48fa88418120dfd9b043f136e08ec73ce3f/exec.fifo`: No such file or directory
[myblog-web] | Error: unable to start container a20111a1b34d1757f96fa10fbec4a48fa88418120dfd9b043f136e08ec73ce3f: `/usr/bin/crun start a20111a1b34d1757f96fa10fbec4a48fa88418120dfd9b043f136e08ec73ce3f` failed: exit status 1
[myblog-e2e] | xUnit.net v3 In-Process Runner v3.2.2+728c1dce01 (64-bit .NET 10.0.1)
[myblog-e2e] |   Discovering: MyBlog.E2E
[myblog-e2e] |   Discovered:  MyBlog.E2E
[myblog-e2e] |   Starting:    MyBlog.E2E
[myblog-e2e] |     MyBlog.E2E.Tests.LoginPageTests.LoginPage_AfterLogin_ShowsLogoutButton [FAIL]
[myblog-e2e] |       System.TimeoutException : Timeout 45000ms exceeded.
[myblog-e2e] |       =========================== logs ===========================
[myblog-e2e] |       waiting for navigation to "**/admin**" until "Load"
[myblog-e2e] |       ============================================================
[myblog-e2e] |       ---- System.TimeoutException : Timeout 45000ms exceeded.
[myblog-e2e] |       Stack Trace:
[myblog-e2e] |         /_/src/Playwright/Core/Waiter.cs(226,0): at Microsoft.Playwright.Core.Waiter.WaitForPromiseAsync[T](Task`1 task, Action dispose)
[myblog-e2e] |         /_/src/Playwright/Core/Frame.cs(321,0): at Microsoft.Playwright.Core.Frame.WaitForNavigationInternalAsync(Waiter waiter, String urlString, Func`2 urlFunc, Regex urlRegex, Nullable`1 waitUntil)
[myblog-e2e] |         /_/src/Playwright/Core/Frame.cs(285,0): at Microsoft.Playwright.Core.Frame.RunAndWaitForNavigationAsync(Func`1 action, FrameRunAndWaitForNavigationOptions options)
[myblog-e2e] |         Tests/LoginPageTests.cs(102,0): at MyBlog.E2E.Tests.LoginPageTests.LoginPage_AfterLogin_ShowsLogoutButton()
[myblog-e2e] |         --- End of stack trace from previous location ---
[myblog-e2e] |         ----- Inner Stack Trace -----
[myblog-e2e] |         /_/src/Playwright/Helpers/TaskHelper.cs(72,0): at Microsoft.Playwright.Helpers.TaskHelper.<>c__DisplayClass2_0.<WithTimeout>b__0()
[myblog-e2e] |         /_/src/Playwright/Helpers/TaskHelper.cs(108,0): at Microsoft.Playwright.Helpers.TaskHelper.WithTimeout(Task task, Func`1 timeoutAction, TimeSpan timeout, CancellationToken cancellationToken)
[myblog-e2e] |         /_/src/Playwright/Core/Waiter.cs(218,0): at Microsoft.Playwright.Core.Waiter.WaitForPromiseAsync[T](Task`1 task, Action dispose)
[myblog-e2e] |     MyBlog.E2E.Tests.LoginPageTests.LoginPage_WithValidCredentials_RedirectsToAdmin [FAIL]
[myblog-e2e] |       System.TimeoutException : Timeout 45000ms exceeded.
[myblog-e2e] |       =========================== logs ===========================
[myblog-e2e] |       waiting for navigation to "**/admin**" until "Load"
[myblog-e2e] |       ============================================================
[myblog-e2e] |       ---- System.TimeoutException : Timeout 45000ms exceeded.
[myblog-e2e] |       Stack Trace:
[myblog-e2e] |         /_/src/Playwright/Core/Waiter.cs(226,0): at Microsoft.Playwright.Core.Waiter.WaitForPromiseAsync[T](Task`1 task, Action dispose)
[myblog-e2e] |         /_/src/Playwright/Core/Frame.cs(321,0): at Microsoft.Playwright.Core.Frame.WaitForNavigationInternalAsync(Waiter waiter, String urlString, Func`2 urlFunc, Regex urlRegex, Nullable`1 waitUntil)
[myblog-e2e] |         /_/src/Playwright/Core/Frame.cs(285,0): at Microsoft.Playwright.Core.Frame.RunAndWaitForNavigationAsync(Func`1 action, FrameRunAndWaitForNavigationOptions options)
[myblog-e2e] |         Tests/LoginPageTests.cs(77,0): at MyBlog.E2E.Tests.LoginPageTests.LoginPage_WithValidCredentials_RedirectsToAdmin()
[myblog-e2e] |         --- End of stack trace from previous location ---
[myblog-e2e] |         ----- Inner Stack Trace -----
[myblog-e2e] |         /_/src/Playwright/Helpers/TaskHelper.cs(72,0): at Microsoft.Playwright.Helpers.TaskHelper.<>c__DisplayClass2_0.<WithTimeout>b__0()
[myblog-e2e] |         /_/src/Playwright/Helpers/TaskHelper.cs(108,0): at Microsoft.Playwright.Helpers.TaskHelper.WithTimeout(Task task, Func`1 timeoutAction, TimeSpan timeout, CancellationToken cancellationToken)
[myblog-e2e] |         /_/src/Playwright/Core/Waiter.cs(218,0): at Microsoft.Playwright.Core.Waiter.WaitForPromiseAsync[T](Task`1 task, Action dispose)
[myblog-e2e] |     MyBlog.E2E.Tests.LoginPageTests.LoginPage_WithInvalidCredentials_ShowsError [FAIL]
[myblog-e2e] |       Microsoft.Playwright.PlaywrightException : Locator expected to be visible
[myblog-e2e] |       Error: element(s) not found 
[myblog-e2e] |       Call log:
[myblog-e2e] |         - Expect "ToBeVisibleAsync" with timeout 30000ms
[myblog-e2e] |         - waiting for Locator(".error, .error-message, .alert, .alert-danger, .validation-error, [class*='error' i], [class*='invalid' i], .text-danger").First
[myblog-e2e] |       Stack Trace:
[myblog-e2e] |         /_/src/Playwright/Core/AssertionsBase.cs(90,0): at Microsoft.Playwright.Core.AssertionsBase.ExpectImplAsync(String expression, FrameExpectOptions expectOptions, Object expected, String message, String title)
[myblog-e2e] |         /_/src/Playwright/Core/AssertionsBase.cs(66,0): at Microsoft.Playwright.Core.AssertionsBase.ExpectImplAsync(String expression, ExpectedTextValue[] expectedText, Object expected, String message, String title, FrameExpectOptions options)
[myblog-e2e] |         Tests/LoginPageTests.cs(61,0): at MyBlog.E2E.Tests.LoginPageTests.LoginPage_WithInvalidCredentials_ShowsError()
[myblog-e2e] |         --- End of stack trace from previous location ---
[myblog-e2e] |   Finished:    MyBlog.E2E (ID = '4d488734a9156ea8719a93bca3e9cb47dfeac4715a7981582060bf185a1e7bf4')
[myblog-e2e] | === TEST EXECUTION SUMMARY ===
[myblog-e2e] |    MyBlog.E2E  Total: 18, Errors: 0, Failed: 3, Skipped: 0, Not Run: 0, Time: 126.263s
[myblog-e2e] | 
[INFO] Capturing logs...
[INFO] Cleaning up...
myblog-e2e
myblog-web
myblog-e2e
myblog-web
myblog_myblog-data
e8bbdd086a0f54b10f15cfb748547ff8571150ab1ef3d3c90ffc14fa7b20ee7b
myblog_myblog-network
[INFO] E2E tests passed! ✓

real	2m45.448s
user	0m23.530s
sys	0m14.184s
kushal@fedora:~/src/dotnet/MyBlog$ 













