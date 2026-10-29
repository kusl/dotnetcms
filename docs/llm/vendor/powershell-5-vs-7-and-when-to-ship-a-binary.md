I write a lot of small automation. Some of it lives as PowerShell scripts on Windows machines at work, some of it runs on my Debian servers, and some of it has turned into real .NET applications. This post is my attempt to write down, in one place, what I wish I had known earlier: what PowerShell actually is in 2026, why there are two of them, what runs where, how to write PowerShell that does not fall apart when it moves between machines, and how to decide when a script should stop being a script and become a compiled command-line application instead.

The worked example for the last part is my [NetworkMonitor](https://github.com/kusl/NetworkMonitor) repository. Every push to it produces a GitHub release containing self-contained executables for six platforms, and you do not need a .NET runtime installed to run them. It is a useful counterpoint to "just write a script", and it is also a useful example of what the "ship a binary" path really costs.

## How to read this post

Every factual claim that comes from documentation links to its source inline, and there is a full source list at the end. Sentences that end with ⚑ are my own judgment, inference, or experience. They are not backed by a citation, and you should weigh them accordingly. Where I say something was "tested", I ran the code while writing this post on PowerShell 7.6.9 on Linux. I did not re-run the Windows PowerShell 5.1 examples for this post, so the 5.1 behavior described here comes from Microsoft's documentation, not from my own test run.

## Two products that share a name

The single most important thing to understand is that "PowerShell" today means two different products. Windows PowerShell 5.1 is built on .NET Framework 4.5. Starting with PowerShell 6.0, PowerShell became an open source project built on .NET Core, and that move is what made it cross-platform: the current PowerShell runs on Windows, macOS, and Linux ([Microsoft Learn: Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).

They also live under different support models. Windows PowerShell is a component of the Windows operating system and follows the Windows support lifecycle, and support for it goes through Windows support channels. PowerShell 7 follows the Microsoft Modern Lifecycle Policy, its end-of-support dates follow the .NET release it was built on, and it is released under the MIT license ([Microsoft Learn: PowerShell Support Lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)).

Windows ships Windows PowerShell in the box. PowerShell 7 has to be downloaded and installed separately ([PowerShell 7 Workshop, Packt](https://packtpub.com/en-sk/product/powershell-7-workshop-9781801812986/chapter/chapter-1-introduction-to-powershell-7-what-it-is-and-how-to-get-it-2/section/what-is-powershell-7-ch02lvl1sec04)). That one fact drives a surprising number of real-world decisions, because the PowerShell you can assume exists on a random Windows machine is 5.1, not 7.

The two install side by side. Windows PowerShell 5.1 lives in `$env:WINDIR\System32\WindowsPowerShell\v1.0` and runs as `powershell.exe`. PowerShell 7 lives in `$env:ProgramFiles\PowerShell\7` and runs as `pwsh.exe`. They keep modules in different locations and use different profile paths ([Microsoft Learn: Migrating from Windows PowerShell 5.1 to PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/migrating-from-windows-powershell-51-to-powershell-7)).

Here is where the PowerShell 7 line stands as of late October 2026, from Microsoft's lifecycle page ([PowerShell Support Lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)):

| Version | Built on | Released | End of support |
|---|---|---|---|
| PowerShell 7.7 (preview) | .NET 11 | preview | not supported |
| PowerShell 7.6 (LTS) | .NET 10 | 18-Mar-2026 | 14-Nov-2028 |
| PowerShell 7.5 | .NET 9 | 23-Jan-2025 | 10-Nov-2026 |
| PowerShell 7.4 (LTS) | .NET 8 | 16-Nov-2023 | 10-Nov-2026 |

If you are on 7.4 or 7.5, note that both reach end of support on 10 November 2026, which at the time of writing is less than two weeks away ([PowerShell Support Lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)). The newest 7.6 servicing build on the [PowerShell GitHub releases page](https://github.com/PowerShell/PowerShell/releases) when I checked was v7.6.9, which is the build I used for the tested examples below. Microsoft only supports the latest update of a given release, so "7.6" in practice means "the newest 7.6.x" ([PowerShell Support Lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)).

To find out which one you are running, check the edition. From PowerShell 5.1 onward, `$PSEdition` and `$PSVersionTable.PSEdition` report `Desktop` for Windows PowerShell on .NET Framework and `Core` for PowerShell on .NET. In PowerShell 4 and earlier the variable does not exist, and a null value should be treated as `Desktop` ([Microsoft Learn: about_PowerShell_Editions](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_editions)).

```powershell
$PSVersionTable.PSVersion
$PSVersionTable.PSEdition
[System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
```

The last line prints the exact .NET version underneath the PowerShell you are running, which Microsoft's lifecycle page suggests for exactly this purpose ([PowerShell Support Lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)).

## PowerShell basics in ten minutes

The idea that makes PowerShell different from bash or cmd is that commands produce objects, not text. Each row of output is an object and each column is a property, so the next command in a pipeline can work with properties directly instead of parsing text by position ([Microsoft Learn training: Review the Windows PowerShell pipeline and its output](https://learn.microsoft.com/en-us/training/modules/understand-windows-powershell-pipeline/2-review-its-output)). That is why this works without a single `awk` or `cut`:

```powershell
Get-Process |
    Where-Object { $_.WorkingSet64 -gt 50MB } |
    Sort-Object -Property WorkingSet64 -Descending |
    Select-Object -First 3 -Property Name, Id, @{ Name = 'WorkingSetMB'; Expression = { [math]::Round($_.WorkingSet64 / 1MB) } }
```

Tested: this runs on PowerShell 7.6.9. `$_` is the current pipeline object, `50MB` and `1MB` are numeric literals PowerShell understands natively, and the hashtable in `Select-Object` creates a calculated property ⚑.

Commands follow a `Verb-Noun` naming convention. `Get-Verb` lists the approved verbs ([Simple Talk: The PoSh DBA, the attributes of advanced functions](https://www.red-gate.com/simple-talk/databases/sql-server/database-administration-sql-server/the-posh-dba-the-attributes-of-advanced-functions/)), and the PSScriptAnalyzer rule `PSUseApprovedVerbs` warns when a function uses one that is not on the list ([Microsoft Learn: Using PSScriptAnalyzer](https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/using-scriptanalyzer?view=ps-modules)). The three discovery commands I use more than any others are `Get-Command` to find a command, `Get-Help` to read about it, and `Get-Member` to see what properties and methods an object actually has ⚑.

The rest of the core language fits on one screen. Everything below was tested on 7.6.9 and uses only syntax that also exists in 5.1 ⚑:

```powershell
$server = @{ Name = 'virginia'; Port = 22 }
$server.Name

$list = @('colorado', 'texas', 'virginia')
$list.Count
$list -contains 'texas'

'Texas' -eq 'texas'
'Texas' -ceq 'texas'

$params = @{ Path = '.'; Filter = '*.ps1' }
Get-ChildItem @params

try {
    Get-Item -Path ./does-not-exist.txt -ErrorAction Stop
}
catch {
    "caught: $($_.Exception.GetType().Name)"
}

git rev-parse --is-inside-work-tree 2>$null
if ($LASTEXITCODE -ne 0) {
    "git exit code $LASTEXITCODE"
}
```

A few things in that block trip up people coming from C#. Comparison operators are words (`-eq`, `-gt`, `-contains`), and string comparison is case-insensitive by default, which is why `'Texas' -eq 'texas'` is true and you need `-ceq` for a case-sensitive check (tested). `@params` is splatting, which passes a hashtable as named parameters. Many cmdlet errors are non-terminating by default, so `try`/`catch` does not see them unless you add `-ErrorAction Stop` or set `$ErrorActionPreference = 'Stop'` ⚑. By default, a native program like `git` failing does not throw an exception; you check `$LASTEXITCODE` instead ⚑. Tested: the `git` call above, run outside a repository, set `$LASTEXITCODE` to 128.

One more basic that confuses Windows users: execution policy. On Windows clients the default is `Restricted`, on Windows Server it is `RemoteSigned`, and on non-Windows platforms it is `Unrestricted` and cannot be changed. Microsoft is explicit that execution policy is not a security system that restricts user actions; it is a guard rail against running scripts unintentionally ([Microsoft Learn: about_Execution_Policies](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies)).

## What works in both

The good news first. Microsoft's own summary is that there are few differences in the PowerShell language between Windows PowerShell and PowerShell; the notable differences are in which cmdlets exist and how they behave, and in the gap between .NET Framework and .NET ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)). The editions documentation says the same thing from the other direction: the language behaves the same across editions, and it is the cmdlets, modules, and .NET APIs you call that create compatibility problems ([about_PowerShell_Editions](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_editions)).

So the portable subset is large. Variables, hashtables, arrays, `[pscustomobject]`, pipelines, `if`/`switch`/`foreach`, `try`/`catch`/`finally`, splatting, script blocks, and comment-based help all behave the same in both ⚑. Advanced functions with `[CmdletBinding()]` work in both, and `CmdletBinding` gives a function the same parameter binding as a compiled cmdlet, the common parameters, and `$PSCmdlet`; `SupportsShouldProcess` adds `-WhatIf` and `-Confirm` ([Microsoft Learn: about_Functions_CmdletBindingAttribute](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_functions_cmdletbindingattribute)). `Set-StrictMode` works in both ([Microsoft Learn: Set-StrictMode](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/set-strictmode)). Calling .NET types directly with `[Type]::Method()` or `New-Object` works in both, with the caveat that the set of available overloads can differ (more on that below).

When a command exists in one edition but not the other, there is usually a replacement that works in both. These are the substitutions I lean on, all taken from Microsoft's differences document ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)):

| Instead of (5.1 only) | Use (works in both) |
|---|---|
| `Get-WmiObject`, `Invoke-WmiMethod`, other WMI v1 cmdlets | `Get-CimInstance` and the other CimCmdlets |
| `Get-EventLog`, `Write-EventLog` | `Get-WinEvent`, `New-WinEvent` |
| `Get-Service -ComputerName` | `Invoke-Command` running `Get-Service` remotely |
| `Invoke-WebRequest` with `.ParsedHtml` | Parse the content yourself; `ParsedHtml` and `Forms` are gone in 7 |

The other portable habit is to never rely on default encodings. Beginning in PowerShell 5.1, the redirection operators call `Out-File`, so `$PSDefaultParameterValues` can set the default encoding for both cmdlets and redirection ([Microsoft Learn: about_Character_Encoding](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_character_encoding?view=powershell-7.6)). Encoding gets its own section below because it is the difference most likely to corrupt data silently.

## What works in Windows PowerShell 5.1 but not in PowerShell 7

These come from Microsoft's differences document unless noted ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)):

| Feature in 5.1 | Status in 7 |
|---|---|
| PowerShell Workflow (`workflow` keyword, PSWorkflow modules) | Removed; Windows Workflow Foundation is not supported on .NET Core |
| PowerShell ISE | Not included |
| Snap-ins (`Add-PSSnapin` and friends) | Custom snap-ins no longer supported |
| WMI v1 cmdlets (`Get-WmiObject` etc.) | Removed; use CimCmdlets |
| `*-EventLog` cmdlets | Removed; use `Get-WinEvent` and `New-WinEvent` |
| `*-Transaction` cmdlets | Removed |
| `New-WebServiceProxy` (SOAP) | Removed; .NET Core does not support WCF for this |
| `Convert-String`, `ConvertFrom-String` | Removed |
| `Export-Counter`, `Import-Counter` | Removed |
| `Add-Computer`, `Remove-Computer`, `Checkpoint-Computer`, restore-point cmdlets | Removed |
| Modules: Microsoft.PowerShell.LocalAccounts, ODataUtils, Operation.Validation, PSScheduledJob | Not included |
| DSC configuration cmdlets in the box | PSDesiredStateConfiguration removed from 7.2 onward and published to the PowerShell Gallery |
| `Invoke-WebRequest` `.ParsedHtml` and `.Forms` | Removed; basic HTML parsing only |
| `file://` and `ftp://` in web cmdlets | Not supported |
| `System.Net.ServicePointManager` settings affecting web cmdlets | No longer honored |
| `-Encoding Byte` on file cmdlets | Removed; use `-AsByteStream` |
| `-Protocol` (DCOM) on `Restart-Computer` and friends | Removed; WSMan only |
| `-ComputerName` on `*-Service` cmdlets | Removed; use `Invoke-Command` |
| `Add-Type -Language VisualBasic` | Removed |
| `Get-FileHash` with `MACTripleDES` or `RIPEMD160` | Algorithms removed from .NET |
| `ClrVersion` and `BuildVersion` in `$PSVersionTable` | Removed |

There is an escape hatch for Windows-only modules. On Windows, `Import-Module -UseWindowsPowerShell` creates a proxy module in PowerShell 7 that runs the module's commands in a local Windows PowerShell process behind the scenes ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)). Modules that ship with Windows are also filtered by their declared edition: PowerShell 7 only exposes system modules marked compatible with `Core`, and `-SkipEditionCheck` overrides that ([about_PowerShell_Editions](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_editions)). In my experience, a proxy module is fine for occasional admin commands but awkward inside a long-running automation, because objects come back through serialization rather than as live .NET objects ⚑.

Some things that people assume are missing actually came back. PowerShell 7.0 restored `Out-GridView`, `Show-Command`, and `Get-Help -ShowWindow` on Windows after .NET Core 3.1 added WPF support ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)), and 7.0 also added back `Get-Counter`, `Clear-RecycleBin`, and `Out-Printer` on Windows, plus `Get-HotFix` and `Update-List` ([Microsoft Learn: What's new in PowerShell 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3)).

## What works in PowerShell 7 but not in Windows PowerShell 5.1

PowerShell 7.0 introduced a group of language features at once: the ternary operator, the pipeline chain operators `&&` and `||`, the null-coalescing operators, `ForEach-Object -Parallel`, a new default error view, and `Get-Error` ([What's new in PowerShell 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3)). The full list from Microsoft's documentation:

| Feature | Example | Source |
|---|---|---|
| Ternary operator | `$x = (Test-Path $p) ? 'yes' : 'no'` | [What's new in 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3) |
| Pipeline chain operators | `dotnet build && dotnet test` and `cmd \|\| Write-Warning 'failed'` | [What's new in 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3) |
| Null-coalescing and assignment | `$port = $cfg ?? 8080`, `$x ??= 100` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| Null-conditional member and index access | `${obj}?.Name`, `${arr}?[0]` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| Parallel pipelines | `ForEach-Object -Parallel { } -ThrottleLimit 5` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| Background a pipeline with a trailing `&` | `Copy-Item $a $b &` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| Unicode escapes in strings | backtick-u followed by a code point in braces | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| ConciseView errors and `Get-Error` | `Get-Error -Newest 3` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| New cmdlets | `Join-String`, `Test-Json`, `Get-Uptime`, `Remove-Alias`, `Remove-Service`, `ConvertFrom-Markdown`, `Show-Markdown` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| `Where-Object -Not` | `Get-Service \| Where-Object -Not DependentServices` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| `Measure-Object` standard deviation | `Measure-Object -Property CPU -AllStats` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| `cd -` back to the previous directory | `cd -` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| Web cmdlet additions | `-Form`, `-Resume`, `-SkipHeaderValidation` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| Cross-platform clipboard, `ConvertFrom-SecureString -AsPlainText` | `Get-Clipboard` on Linux | [What's new in 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3) |
| Remoting over SSH, including `user@host:port` | `Enter-PSSession -HostName me@host:2222` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| `Update-Help` without admin rights | `Update-Help` | [Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) |
| Linux, macOS, ARM32 and ARM64 | `pwsh` on a Debian server or an ARM64 board | [What's new in 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3) |

The new operators are not like a missing cmdlet. A missing cmdlet fails when execution reaches that line. A `?:` or `&&` in a script run by Windows PowerShell 5.1 is a syntax error, and because PowerShell parses the whole script before running it, nothing in the file runs at all ⚑. If a script uses any of these operators, put `#Requires -Version 7.0` (or higher) at the top so 5.1 users get a clear message instead of a confusing parse error ⚑.

Here is a tested example of the new operators, including one trap worth knowing about:

```powershell
$config = [pscustomobject]@{ Port = 9000 }

$config?.Port ?? 8080
${config}?.Port ?? 8080

$mode = (Test-Path ./settings.json) ? 'file' : 'defaults'

git --version > $null && 'git ok' || 'git missing'

1..5 | ForEach-Object -Parallel {
    "item $_ on thread $([System.Threading.Thread]::CurrentThread.ManagedThreadId)"
} -ThrottleLimit 5
```

The first null-conditional line returns 8080, not 9000. Because `?` is a legal character in a PowerShell variable name, `$config?.Port` is parsed as the property `Port` of a variable named `config?`, which does not exist; you have to write `${config}?.Port` (tested on 7.6.9). Microsoft documents this requirement for braces ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)), but the failure mode is silent, which makes it worse than an error. With `Set-StrictMode -Version 3.0` turned on, referencing the uninitialized variable would at least throw instead of quietly producing the fallback ⚑.

The pipeline chain operators decide success using `$?` and `$LASTEXITCODE`, so they work with native programs like `git` and `dotnet`, not only with cmdlets ([What's new in PowerShell 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3)). With `ForEach-Object -Parallel`, use `$using:` to pass outer variables into the parallel script block, and the default throttle limit is 5 ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).

## Same script, different results

The removed cmdlets and new operators are loud. The dangerous differences are the quiet ones, where the same script runs on both editions and produces different output.

### Character encoding

This is the big one. In Windows PowerShell, `Out-File` and the redirection operators `>` and `>>` write UTF-16LE, `Set-Content` and `Add-Content` on a new file use the system's legacy ANSI code page, `-Encoding UTF8` writes a byte-order mark, and the defaults are not consistent between cmdlets. PowerShell 6 and later default to UTF-8 without a BOM for all text output ([Microsoft Learn: about_Character_Encoding](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_character_encoding?view=powershell-7.6)).

| Operation | Windows PowerShell 5.1 | PowerShell 7 |
|---|---|---|
| `Out-File`, `>`, `>>` | UTF-16LE | UTF-8, no BOM |
| `Set-Content` to a new file, no `-Encoding` | ANSI code page | UTF-8, no BOM |
| `-Encoding utf8` | UTF-8 with BOM | UTF-8, no BOM |
| UTF-8 with BOM explicitly | `-Encoding UTF8` | `-Encoding utf8BOM` |
| `Get-Content` on a file without a BOM | Reads as ANSI | Reads as UTF-8 ⚑ |
| Script file saved as UTF-8 without BOM containing non-ASCII | May be misread as ANSI | Fine |
| `$OutputEncoding` for piping to native programs | ASCII | UTF-8, no BOM |

Sources for that table: [about_Character_Encoding](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_character_encoding?view=powershell-7.6) for the file behavior, and [Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6) for the `$OutputEncoding` change. I tested the PowerShell 7 column for `-Encoding utf8`: writing `héllo` produced first bytes 104, 195, 169, which is the letter h followed by the two-byte UTF-8 encoding of é, with no BOM.

The script-file row deserves emphasis. Microsoft's guidance is that if a script needs non-ASCII characters and must run on Windows PowerShell, save it as UTF-8 with a BOM, because without the BOM Windows PowerShell reads the script in the legacy ANSI code page. The catch is that a BOM can upset Unix tools such as `cat`, `sed`, and `awk` ([about_Character_Encoding](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_character_encoding?view=powershell-7.6)). My own rule is to keep script source pure ASCII when it has to run on both, and to write every file with an explicit `-Encoding` ⚑. The emoji status symbols in NetworkMonitor's console output are an example of something I would not put in a cross-edition PowerShell script ⚑.

### .NET method overloads

Because PowerShell exposes .NET directly, a newer .NET can change which overload a call binds to. The documented example is `String.Split`: in 5.1, `"1111p2222q3333".Split('pq')` splits on either character, but in 7 the string binds to the new string-separator overload and nothing is split. Casting to `[char[]]` restores the old behavior ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)). Tested on 7.6.9: the uncast call returned the original string unchanged, and the `[char[]]` version returned three parts. The `-split` operator with a regex, such as `"1111p2222q3333" -split '[pq]'`, avoids the ambiguity entirely and behaves the same in both editions ⚑.

### Errors and native commands

In PowerShell 7, `$?` is no longer set to false just because a native program wrote to stderr; only a non-zero exit code does that. `$ErrorActionPreference` also no longer applies to stderr output from native commands ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)). This matters for tools like `git` that write progress to stderr. A 5.1 script with `$ErrorActionPreference = 'Stop'` can fail on a successful `git` command that 7 runs without complaint ⚑. In both editions, the reliable check is `$LASTEXITCODE` ⚑.

Also in 7, `-Verbose` and `-Debug` no longer override `$ErrorActionPreference`, and `-Debug` sets `$DebugPreference` to `Continue` instead of `Inquire` ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).

### Smaller behavior changes

All of these are documented in Microsoft's differences page or the 7.0 release notes, and each can change a script's output without any error:

- `Export-Csv` no longer writes a `#TYPE` header line by default; use `-IncludeTypeInformation` for the old behavior ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- `Group-Object` now returns its groups sorted ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- `ConvertFrom-Json` unwraps collections by default ([What's new in PowerShell 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3)).
- `Get-Content -Delimiter` no longer includes the delimiter in returned lines ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- From 7.1, culture-invariant string comparison ignores non-printing control characters, so a string containing a bell character can compare equal to one without it ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- When a function is called with splatting and also with an explicit named argument, the explicit argument now wins, which changes the order of values left in `$args` ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- `Invoke-WebRequest` and `Invoke-RestMethod` are built on `HttpClient`, header values are string arrays, header validation is strict by default, and sending `-Credential` over plain `http://` is an error unless you add `-AllowUnencryptedAuthentication` ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- `Invoke-RestMethod` returns `$null` for a JSON `null` response instead of the string "null" ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- The executable's first positional parameter changed from `-Command` to `-File`, so `pwsh foo.ps1` runs a script and `pwsh Get-Command` does not run a command; use `pwsh -c` for commands ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).
- PowerShell 7 sends basic telemetry at startup, and the only way to opt out is the `POWERSHELL_TELEMETRY_OPTOUT` environment variable ([Differences](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)).

## How to write modern PowerShell

### Pick a target edition on purpose

My default is PowerShell 7.6 LTS for anything new, and Windows PowerShell 5.1 only when the script must run on Windows machines where I cannot install `pwsh` ⚑. Microsoft's own pitch for moving is that PowerShell 7 installs side by side with 5.1, so it can be tested before it replaces anything ([Microsoft Learn: Migrating from Windows PowerShell 5.1 to PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/migrating-from-windows-powershell-51-to-powershell-7)). If you do need both, Microsoft is direct that the only real way to confirm compatibility is to write tests and run them on every edition you support, and it recommends Pester for that ([about_PowerShell_Editions](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_editions)).

### A template that runs on both

This is the shape I start from when a script has to run on 5.1 and 7. It is tested on 7.6.9 and uses no 7-only syntax ⚑:

```powershell
#Requires -Version 5.1
<#
.SYNOPSIS
    Removes log files older than a cutoff. Runs on Windows PowerShell 5.1 and PowerShell 7.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string] $Path,

    [ValidateRange(1, 3650)]
    [int] $OlderThanDays = 30
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$cutoff = (Get-Date).AddDays(-$OlderThanDays)

Get-ChildItem -LiteralPath $Path -Filter '*.log' -File |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    ForEach-Object {
        if ($PSCmdlet.ShouldProcess($_.FullName, 'Remove old log')) {
            Remove-Item -LiteralPath $_.FullName
            [pscustomobject]@{
                Path      = $_.FullName
                LastWrite = $_.LastWriteTime
                Removed   = $true
            }
        }
    }
```

Running it with `-WhatIf` printed "What if: Performing the operation "Remove old log" on target ..." and deleted nothing; running it without `-WhatIf` removed only the 60-day-old file and emitted one object describing it (tested).

### The rules behind the template

- Declare the minimum version with `#Requires` so the wrong edition fails immediately with a clear message ⚑.
- Use `[CmdletBinding()]` and a `param()` block with validation attributes, so binding errors happen before any work starts; with `CmdletBinding`, unknown parameters cause binding to fail instead of landing silently in `$args` ([about_Functions_CmdletBindingAttribute](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_functions_cmdletbindingattribute)).
- Anything that changes state gets `SupportsShouldProcess` and a `$PSCmdlet.ShouldProcess()` check, which gives you `-WhatIf` and `-Confirm` for free ([about_Functions_CmdletBindingAttribute](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_functions_cmdletbindingattribute)).
- Turn on strict mode, but pin the version. `Set-StrictMode -Version 3.0` prohibits uninitialized variables, non-existent properties, method-style function calls, and out-of-bounds indexes. Microsoft warns that `-Version Latest` is not deterministic, because a script written for one PowerShell version can become subject to stricter rules on a newer one ([Set-StrictMode](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/set-strictmode)).
- Set `$ErrorActionPreference = 'Stop'` at the top of automation scripts and use `try`/`catch`, so a failed step stops the script instead of letting later steps run on bad state ⚑.
- Emit objects, not formatted strings. Return `[pscustomobject]` and let the caller decide whether to show a table, export CSV, or convert to JSON ⚑. Keep `Write-Host` for messages that are only meant for a human at a console ⚑.
- Use `-LiteralPath` when a path comes from data, because `-Path` treats square brackets and other characters as wildcards ⚑.
- Always pass `-Encoding` when writing files, or set it once through `$PSDefaultParameterValues` ([about_Character_Encoding](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_character_encoding?view=powershell-7.6)).
- Check `$LASTEXITCODE` after every native command whose failure matters ⚑.
- Use approved verbs (`Get-Verb`) and full cmdlet names instead of aliases in scripts; PSScriptAnalyzer has rules for both, `PSUseApprovedVerbs` and `PSAvoidUsingCmdletAliases` ([Using PSScriptAnalyzer](https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/using-scriptanalyzer?view=ps-modules)).
- When the same functions are used by more than one script, turn them into a module with a manifest, and declare `CompatiblePSEditions = @('Desktop', 'Core')` if you have tested both. Omitting the field is treated as `Desktop` ([about_PowerShell_Editions](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_editions)).

### Lint it

PSScriptAnalyzer is the static analyzer for PowerShell. It supports a `PSScriptAnalyzerSettings.psd1` settings file that it discovers automatically in the project root, built-in presets such as `PSGallery` and `CodeFormatting`, and compatibility rules that check a script against other PowerShell versions and platforms ([Using PSScriptAnalyzer](https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/using-scriptanalyzer?view=ps-modules)). PSScriptAnalyzer 1.18 and later can flag commands and .NET APIs that may be incompatible across editions ([about_PowerShell_Editions](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_editions)).

```powershell
Install-Module -Name PSScriptAnalyzer -Scope CurrentUser
Invoke-ScriptAnalyzer -Path . -Recurse -Settings PSGallery
```

I treat a PSScriptAnalyzer warning the way I treat a C# analyzer warning in a project with `TreatWarningsAsErrors`: fix it or suppress it deliberately with a reason, but do not leave it sitting there ⚑.

## When to stop scripting and ship a binary

Now the question I actually wanted to answer. PowerShell and a compiled .NET command-line app are closer relatives than they look. PowerShell 7.6 is itself built on .NET 10 ([PowerShell Support Lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)), and PowerShell gives you direct access to the underlying .NET types and methods ([Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)). The difference is not "what can it do", it is how the code is checked, tested, structured, and delivered ⚑.

### The same ping, two ways

NetworkMonitor's [PingService.cs](https://github.com/kusl/NetworkMonitor/blob/master/src/NetworkMonitor.Core/Services/PingService.cs) uses `System.Net.NetworkInformation.Ping`. PowerShell can use that exact class. This script is the "PowerShell version" of NetworkMonitor's core loop, and it runs on both editions:

```powershell
#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory, ValueFromPipeline)]
    [ValidateNotNullOrEmpty()]
    [string[]] $Target,

    [ValidateRange(1, 100)]
    [int] $Count = 3,

    [ValidateRange(100, 60000)]
    [int] $TimeoutMs = 2000
)

begin {
    Set-StrictMode -Version 3.0
    $ErrorActionPreference = 'Stop'
}

process {
    foreach ($name in $Target) {
        $rtts = New-Object System.Collections.Generic.List[long]
        $ping = New-Object System.Net.NetworkInformation.Ping
        try {
            for ($i = 0; $i -lt $Count; $i++) {
                try {
                    $reply = $ping.Send($name, $TimeoutMs)
                    if ($reply.Status -eq [System.Net.NetworkInformation.IPStatus]::Success) {
                        $rtts.Add($reply.RoundtripTime)
                    }
                }
                catch [System.Net.NetworkInformation.PingException] {
                    Write-Verbose "Ping to $name failed: $($_.Exception.Message)"
                }
            }
        }
        finally {
            $ping.Dispose()
        }

        $avg = $null
        if ($rtts.Count -gt 0) {
            $avg = [math]::Round(($rtts | Measure-Object -Average).Average, 1)
        }

        [pscustomobject]@{
            Target      = $name
            Sent        = $Count
            Received    = $rtts.Count
            LossPercent = [math]::Round(100 * ($Count - $rtts.Count) / $Count)
            AvgMs       = $avg
        }
    }
}
```

Tested on 7.6.9: `-Target 127.0.0.1, 'no-such-host.invalid' -Count 2` returned 0% loss for loopback and 100% loss with a null average for the unresolvable host, and piping `'127.0.0.1','localhost'` into the script returned one object per host through `ValueFromPipeline`. Save it as `Test-Reachability.ps1`, run it in a loop with `Start-Sleep`, and you have a working network monitor in about 50 lines ⚑.

For checking a few hosts from my own terminal, that script is the right tool. It is readable, it needs no build, and on any machine with PowerShell it just runs ⚑. The question is what happens when the requirements grow.

### What NetworkMonitor actually does

I have described NetworkMonitor as "basically just ping" before, and that undersells it. Reading the code again for this post, it is a hosted .NET 10 console application with these responsibilities, all visible in the [repository](https://github.com/kusl/NetworkMonitor):

- Auto-detects the default gateway, scoring network interfaces so a physical Ethernet or Wi-Fi adapter wins over VPN tunnels, Docker and WSL bridges, and hypervisor adapters ([GatewayDetector.cs](https://github.com/kusl/NetworkMonitor/blob/master/src/NetworkMonitor.Core/Services/GatewayDetector.cs)).
- Pings the router, a primary internet target with fallbacks, and 48 custom targets configured in [appsettings.json](https://github.com/kusl/NetworkMonitor/blob/master/src/NetworkMonitor.Console/appsettings.json), with bounded concurrency.
- Resolves each host name once per round and reuses the IP for every ping in that round, preferring IPv4 for comparable latency ([PingService.cs](https://github.com/kusl/NetworkMonitor/blob/master/src/NetworkMonitor.Core/Services/PingService.cs)).
- Runs on a start-to-start cadence in a `BackgroundService` with cancellation, and warns once if a cycle overruns its interval ([MonitorBackgroundService.cs](https://github.com/kusl/NetworkMonitor/blob/master/src/NetworkMonitor.Core/Services/MonitorBackgroundService.cs)).
- Classifies health into five states, and deliberately does not penalize a router that answers pings slowly, because consumer routers commonly de-prioritize ICMP on their control-plane CPU ([README](https://github.com/kusl/NetworkMonitor/blob/master/README.md)).
- Stores every measurement in a normalized SQLite schema in WAL mode with retention pruning and incremental vacuum ([README](https://github.com/kusl/NetworkMonitor/blob/master/README.md)).
- Exports OpenTelemetry metrics to rotating JSON files ([README](https://github.com/kusl/NetworkMonitor/blob/master/README.md)).
- Optionally replicates hourly per-target rollups, not raw rows, to a libSQL or Turso database and to a Databricks SQL warehouse, each in its own fault-isolated background service that can never interrupt monitoring ([ServiceCollectionExtensions.cs](https://github.com/kusl/NetworkMonitor/blob/master/src/NetworkMonitor.Core/ServiceCollectionExtensions.cs), [README](https://github.com/kusl/NetworkMonitor/blob/master/README.md)).
- Ships with 73 C# files and 111 test methods marked `[Fact]` or `[Theory]` using hand-written fakes instead of a mocking library, built with warnings as errors and `AnalysisLevel` set to `latest-recommended` ([Directory.Build.props](https://github.com/kusl/NetworkMonitor/blob/master/src/Directory.Build.props), counts taken from the repository export at commit 7f67b54).

Could PowerShell do each of those things? Individually, yes, mostly by calling the same .NET libraries ⚑. Would I want to maintain a PowerShell script that does all of them, with two concurrent replication sinks, a SQLite schema upgrade path, and a test suite? No ⚑. That is the real line: not capability, but how much structure the code needs to stay correct ⚑.

### The release pipeline

The part that inspired this post is [release.yml](https://github.com/kusl/NetworkMonitor/blob/master/.github/workflows/release.yml). On every push to any branch it does the following:

1. Builds on a matrix of native runners for six runtime identifiers: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, and `linux-arm64`.
2. Installs .NET 10 with Microsoft's official install scripts instead of a setup action.
3. Runs `dotnet publish` with `--self-contained true`, `PublishSingleFile=true`, `PublishTrimmed=false`, `IncludeNativeLibrariesForSelfExtract=true`, and debug symbols turned off.
4. Packs each platform as a `.zip` on Windows or a `.tar.gz` elsewhere and uploads it as an artifact.
5. Generates a version of the form `v1.0.YYYYMMDD.HHMMSS`, a `SHA256SUMS.txt`, and release notes built by streaming known-safe pieces into a file rather than using text substitution.
6. Creates the release and tag with `gh release create --latest`, never as a pre-release.

The GitHub API showed 53 releases when I checked. The newest, [v1.0.20261029.140621](https://github.com/kusl/NetworkMonitor/releases/tag/v1.0.20261029.140621), contains six archives between 30.4 MB and 33.5 MB, about 190 MB per release in total. I downloaded the `linux-x64` archive: it contains exactly two files, a 77 MB `NetworkMonitor.Console` executable and `appsettings.json`. In a Linux container with no `dotnet` on the `PATH`, the executable started and ran (tested).

One thing I noticed while reading the workflow for this post: the release workflow does not run the tests. Tests run in the separate [build-and-test.yml](https://github.com/kusl/NetworkMonitor/blob/master/.github/workflows/build-and-test.yml) workflow on Ubuntu, Windows, and macOS, but the two workflows are independent, so a commit that fails its tests still gets a release. For a personal tool that is a tolerable trade-off; for anything other people depend on, I would make the release job depend on the test job ⚑.

### What "no runtime required" really means

Self-contained deployment is real, but it is not magic, and the fine print is worth knowing before you promise anyone "just download and run".

- **You still depend on the operating system.** The `linux-x64` binary is a dynamically linked ELF executable that uses the system's glibc (checked with `file` on the downloaded binary). On Linux, .NET relies on the ICU library for globalization data, and if ICU is not installed the application fails to start, unless you opt into globalization-invariant mode, which gives up culture-aware behavior ([dotnet/corefx: Globalization invariant mode](https://github.com/dotnet/corefx/blob/baa9ed4587f0ba2ee60189246f66c24d46c2cfbf/Documentation/architecture/globalization-invariant-mode.md)).
- **Single file can still write files.** Single-file publishing does not bundle native libraries by default; `IncludeNativeLibrariesForSelfExtract` embeds them and extracts them to a directory on the user's machine at run time, by default under `$HOME/.net` on Linux and `%TEMP%/.net` on Windows, which can be redirected with `DOTNET_BUNDLE_EXTRACT_BASE_DIR` ([Microsoft Learn: Single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file)). After my test run, the SQLite native library `libe_sqlite3.so` was sitting in `~/.net/NetworkMonitor.Console/` (tested). On a locked-down machine where the home directory or temp is not writable, that matters ⚑.
- **You own runtime patching.** Microsoft's .NET support policy notes that apps using the self-contained deployment model are responsible for keeping the runtime updated ([.NET and .NET Core Support Policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)). A machine-wide .NET install gets patched by its package manager or Windows Update; a self-contained binary only gets the security fix when you rebuild it and the user downloads the new build. NetworkMonitor's release-on-every-push design helps here, because any push picks up the latest .NET 10 patch from the install script, but users still have to download it ⚑.
- **Size.** Including the runtime is why the Linux binary is 77 MB for a network monitor. Microsoft notes that self-contained single files are large because they include the runtime and framework libraries, and that trimming can reduce size for trim-compatible apps ([Single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file)). NetworkMonitor publishes with `PublishTrimmed=false`; I have not measured how much trimming would save or whether the hosting and configuration-binding code would survive it ⚑.
- **Some APIs behave differently.** `Assembly.CodeBase` throws in a single-file app; use `AppContext.BaseDirectory` to find files next to the executable ([Single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file)).
- **ICMP may need privileges.** NetworkMonitor's README notes that raw socket access on Linux can require `sudo` or `setcap cap_net_raw+ep` on the binary ([README](https://github.com/kusl/NetworkMonitor/blob/master/README.md)). A compiled binary does not get to skip the operating system's security model ⚑.
- **Supported until when.** .NET 10 is an LTS release, released 11 November 2025 and supported until 14 November 2028 ([.NET and .NET Core Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)). A binary built today keeps running after that date, but it stops getting runtime security fixes unless you move it to a newer .NET ⚑.

### The options between a script and a full project

The choice is not binary. .NET 10 added file-based apps: you can run a single `.cs` file with `dotnet run app.cs`, reference NuGet packages with `#:package` directives at the top of the file, and, on Unix, make the file directly executable with a shebang line. When it grows, `dotnet project convert` turns it into a normal project ([.NET Blog: Announcing dotnet run app.cs](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/), [Microsoft Learn: Build file-based C# programs](https://learn.microsoft.com/dotnet/csharp/fundamentals/tutorials/file-based-programs)). The shebang support is Unix-only; on Windows you run it with `dotnet run` ([Build file-based C# programs](https://learn.microsoft.com/dotnet/csharp/fundamentals/tutorials/file-based-programs)). The catch is that file-based apps need the .NET SDK on the machine that runs them, which is a bigger prerequisite than PowerShell ⚑.

At the other end, Native AOT compiles to native code at publish time, producing a self-contained app with faster startup and a smaller memory footprint that runs without a .NET runtime installed. It has real limits: no dynamic assembly loading, no runtime code generation with `System.Reflection.Emit`, trimming is required, and on Linux a binary built on one distribution version only runs on that version or newer ([Microsoft Learn: Native AOT deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)). Whether NetworkMonitor's dependencies, including OpenTelemetry and the SQLite provider, would publish cleanly as Native AOT is something I have not tried ⚑.

The menu, roughly from lightest to heaviest:

| Option | Needs on the target machine | Good for |
|---|---|---|
| PowerShell script | PowerShell (5.1 on Windows by default, or `pwsh`) | Glue, admin tasks, one-off automation ⚑ |
| PowerShell module | PowerShell | Reusable functions shared by several scripts ⚑ |
| .NET 10 file-based app | .NET 10 SDK | C#-native scripts, prototypes ⚑ |
| Framework-dependent .NET app | Matching .NET runtime | Internal tools where the runtime is already managed ⚑ |
| Self-contained single file | OS libraries only (glibc and ICU on Linux) | Tools you hand to people, like NetworkMonitor ⚑ |
| Native AOT | OS libraries only | Fast startup, small memory, AOT-compatible dependencies ⚑ |

### My decision rules

Everything in this section is my opinion, built from writing both kinds of tools. None of it is sourced, so all of it is marked ⚑.

Reach for PowerShell when:

- The job is orchestration: calling other programs, moving files, querying Windows, Active Directory, Exchange, Azure, or a REST API, and passing results between them ⚑.
- The person running it is an administrator who is comfortable reading and editing the script, and editing it in place on a server is a feature, not a risk ⚑.
- The target machines are Windows and you can count on Windows PowerShell 5.1 being present, or you control the fleet and can install `pwsh` ⚑.
- The script is short-lived, runs on demand or on a schedule, and finishes in seconds or minutes ⚑.
- The whole thing fits on a couple of screens and has one main path through it ⚑.

Reach for a compiled CLI when:

- It runs continuously as a service or a monitor and needs careful handling of cancellation, timing, concurrency, and resource lifetimes ⚑.
- It has real internal structure: several components, persistent storage, schema changes, background workers, or plugins ⚑.
- You need the compiler and analyzers to catch mistakes, and a unit-test suite with fakes you trust ⚑.
- You are handing it to people who should not need to know what PowerShell is, or who are on machines where you cannot install anything ⚑.
- Startup time or memory use matters, which is where Native AOT earns its constraints ⚑.
- The same logic needs to be a library as well as a tool. NetworkMonitor's core is a class library with a thin console host, so the same services could sit behind a web dashboard later ⚑.

A quick test I use: if I am about to write a PowerShell class hierarchy, a retry policy with backoff, or a hand-rolled schema migration, the script has already become an application, and it should move to C# ⚑. In the other direction, if a C# console app is mostly `Process.Start` calls stitched together, it probably wanted to be a PowerShell script ⚑.

### What about Go, Rust, or C?

The same reasoning applies to any compiled language that produces a single executable ⚑. Go and Rust are popular for command-line tools largely because a single self-contained executable is the default output of their toolchains rather than an option you configure ⚑. The question to ask is the same as with .NET: what does the user's machine need, how do you ship security fixes for everything bundled inside the binary, and is your team more productive in that language than in the one they already use every day ⚑. For me, with a .NET background and NetworkMonitor's pipeline already working, a self-contained .NET 10 binary is the path of least resistance, and I do not think rewriting it in another language would make it meaningfully better ⚑. I have not benchmarked NetworkMonitor against an equivalent Go or Rust implementation, so I am not making any size or performance claims here ⚑.

### Was it overkill?

For a tool whose job is to tell me whether my internet is working, a six-platform release on every push is more machinery than strictly necessary ⚑. About 190 MB of archives per release adds up quickly across 53 releases, and a release that does not wait for tests is a shortcut I would not take on a team ⚑. But the pipeline is the reusable part. It is about 300 lines of YAML that turns any .NET console project into downloadable binaries for Windows, macOS, and Linux on x64 and ARM64 ⚑. Once that exists, "should this be a script or an app?" becomes a question about the code, not about how painful distribution will be, and that is the right question to be asking ⚑.

## Summary

- There are two PowerShells. Windows PowerShell 5.1 ships with Windows and runs on .NET Framework. PowerShell 7 is cross-platform, runs on .NET, and the current LTS is 7.6 on .NET 10, supported until November 2028.
- The language is nearly identical. The differences are in cmdlets, modules, .NET APIs, and defaults, especially file encoding.
- 5.1-only features are mostly Windows-legacy: Workflow, snap-ins, WMI v1, EventLog cmdlets, ISE, SOAP proxies. `Import-Module -UseWindowsPowerShell` covers some gaps on Windows.
- 7-only features include the ternary, pipeline chain, and null operators, `ForEach-Object -Parallel`, SSH remoting, and a long list of new and improved cmdlets. Using any of the new operators makes a script unparseable in 5.1 ⚑.
- Modern PowerShell means advanced functions, validated parameters, `ShouldProcess`, pinned strict mode, explicit encodings, objects as output, PSScriptAnalyzer, and Pester on every edition you support.
- Reach for a compiled CLI when the code needs structure, tests, long-running reliability, or distribution to people who should not have to install anything. Self-contained .NET removes the runtime prerequisite, but not the OS libraries, the extraction directory, the binary size, or your responsibility for patching the runtime inside it.

## Sources

- [Microsoft Learn: Differences between Windows PowerShell 5.1 and PowerShell 7.x](https://learn.microsoft.com/en-us/powershell/scripting/whats-new/differences-from-windows-powershell?view=powershell-7.6)
- [Microsoft Learn: What's new in PowerShell 7.0](https://learn.microsoft.com/en-us/previous-versions/powershell/scripting/whats-new/what-s-new-in-powershell-70?view=powershell-7.3)
- [Microsoft Learn: PowerShell Support Lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)
- [Microsoft Learn: Migrating from Windows PowerShell 5.1 to PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/migrating-from-windows-powershell-51-to-powershell-7)
- [Microsoft Learn: about_Character_Encoding](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_character_encoding?view=powershell-7.6)
- [Microsoft Learn: about_PowerShell_Editions](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_editions)
- [Microsoft Learn: about_Execution_Policies](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies)
- [Microsoft Learn: about_Functions_CmdletBindingAttribute](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_functions_cmdletbindingattribute)
- [Microsoft Learn: Set-StrictMode](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/set-strictmode)
- [Microsoft Learn: Using PSScriptAnalyzer](https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/using-scriptanalyzer?view=ps-modules)
- [Microsoft Learn training: Review the Windows PowerShell pipeline and its output](https://learn.microsoft.com/en-us/training/modules/understand-windows-powershell-pipeline/2-review-its-output)
- [PowerShell 7 Workshop, Packt: What is PowerShell 7?](https://packtpub.com/en-sk/product/powershell-7-workshop-9781801812986/chapter/chapter-1-introduction-to-powershell-7-what-it-is-and-how-to-get-it-2/section/what-is-powershell-7-ch02lvl1sec04)
- [Simple Talk: The PoSh DBA, the attributes of advanced functions](https://www.red-gate.com/simple-talk/databases/sql-server/database-administration-sql-server/the-posh-dba-the-attributes-of-advanced-functions/)
- [PowerShell releases on GitHub](https://github.com/PowerShell/PowerShell/releases)
- [Microsoft Learn: Single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file)
- [Microsoft Learn: Native AOT deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [.NET and .NET Core Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [dotnet/corefx: Globalization invariant mode](https://github.com/dotnet/corefx/blob/baa9ed4587f0ba2ee60189246f66c24d46c2cfbf/Documentation/architecture/globalization-invariant-mode.md)
- [.NET Blog: Announcing dotnet run app.cs](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/)
- [Microsoft Learn: Build file-based C# programs](https://learn.microsoft.com/dotnet/csharp/fundamentals/tutorials/file-based-programs)
- [kusl/NetworkMonitor repository](https://github.com/kusl/NetworkMonitor)
- [NetworkMonitor release workflow](https://github.com/kusl/NetworkMonitor/blob/master/.github/workflows/release.yml)
- [NetworkMonitor build and test workflow](https://github.com/kusl/NetworkMonitor/blob/master/.github/workflows/build-and-test.yml)
- [NetworkMonitor release v1.0.20261029.140621](https://github.com/kusl/NetworkMonitor/releases/tag/v1.0.20261029.140621)
