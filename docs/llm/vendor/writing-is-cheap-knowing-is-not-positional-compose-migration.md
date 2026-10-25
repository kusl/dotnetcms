Writing Is Cheap, Knowing Is Not: A Fragments-to-Compose Migration Read Through Dan Luu and Hacker News

Dan Luu says cheap AI coding means we should attempt work we used to skip; Hacker News asked who checks the result. I tested both by having Claude migrate a finished Android app from fragments to Jetpack Compose in about 42 hours. Builds broke often, a green build crashed on launch, a clock hand vanished unnoticed, and a map fallback I added for testing still fails on my tablet. Here is what went wrong, why, and what to change.

---

## Why this post exists

I already wrote about this experiment once on this site, in [Ambition, Verification and a Finished Android App](https://kush.runasp.net/post/ambition-verification-and-a-finished-android-app-what-dan-luu-hacker-news-and-a-fragments-to-compose-migration-say-about-each-other). That post was mostly about the shape of the work: the phases, the safety nets, the provenance. It was too kind. When I wrote the prompt for it, I left out the parts that hurt, and an article is only as honest as the evidence you feed it.

This post is meant to stand on its own, so it repeats what is worth repeating. But its main job is to put the failures on the table and ask the uncomfortable question directly: **this was, on paper, a small and well-understood change. Why did it go wrong as often as it did, and was that my fault?**

The three ingredients:

1. **The essay.** Dan Luu, [We should be more ambitious with the software we build](https://danluu.com/ambitious/), October 2026.
2. **The discussion.** The [Hacker News thread](https://news.ycombinator.com/item?id=50225695) on that essay, submitted by `ltratt`, which had about 458 points and 426 comments when I read it.
3. **The experiment.** My fork [`kusl/hamza417-positional`](https://github.com/kusl/hamza417-positional) of Hamza Rizwan's [`Hamza417/Positional`](https://github.com/Hamza417/Positional), an Android compass, GPS, clock, level, trail and measurement app, migrated from fragments and XML views to Jetpack Compose with Claude doing the writing.

---

## Part 1: Terms used in this article

Some of this is Android-specific and some of it is jargon from the AI-assisted coding world. If you know all of it, skip ahead.

### Android and Kotlin terms

- **Activity.** A single screen-level entry point of an Android app, managed by the operating system. Historically every screen was an activity; modern apps often have one activity and switch content inside it.
- **View system / XML layouts.** Android's original UI toolkit. You describe a screen in XML (`res/layout/*.xml`), the system "inflates" it into a tree of `View` objects, and your code finds those objects and mutates them (set this text, hide that button). This is called *imperative* UI.
- **Fragment.** A reusable chunk of UI with its own lifecycle that lives inside an activity. Fragments were Google's answer to "one activity, many panels" and to tablet layouts. They are also notorious for lifecycle complexity: a fragment has a lifecycle, its view has a separate lifecycle, and getting those two confused is a classic source of crashes and leaks.
- **Jetpack Compose.** Android's modern *declarative* UI toolkit. Instead of mutating views, you write Kotlin functions marked `@Composable` that describe what the screen should look like for a given state. When the state changes, Compose re-runs the affected functions. That re-run is called **recomposition**.
- **Modifier.** In Compose, a chain of instructions attached to an element that controls its size, padding, drawing, gestures and so on. `Modifier.size(48.dp)` sets width and height; `Modifier.height(48.dp)` sets only the height and leaves the width to whatever the content wants. That one-word difference matters later.
- **`painterResource`.** The Compose function that loads an image from the app's `res/drawable` folder. It supports vector drawables and bitmaps. It does **not** support the other kinds of XML drawables the View system happily accepts, such as `<shape>` or `<layer-list>`; given one of those, it throws an exception at runtime.
- **ViewModel.** An object that holds screen state and survives configuration changes such as rotation.
- **LiveData and StateFlow.** Two ways for a ViewModel to publish changing values. `LiveData` is the older Android-specific one; `StateFlow` is the Kotlin coroutines equivalent that Compose prefers.
- **Navigation Compose.** A library that moves between screens inside one activity using named routes.
- **Glance.** A Jetpack library for building home-screen widgets with a Compose-like API.
- **Room.** Android's SQLite database library. Room can export a JSON description of each database schema, including an **identity hash**. If the hash changes, the on-disk format changed, and existing users need a migration or they lose data.
- **Gradle, AGP, `compileSdk`.** Gradle is the build tool; the Android Gradle Plugin (AGP) teaches it how to build Android apps. `compileSdk` is the Android API level you compile against. Newer libraries often demand a newer `compileSdk`, and the `checkDebugAarMetadata` build step fails when they do.
- **Transitive dependency.** A library you get because another library you use depends on it. If you remove the parent, the child disappears too, and any code that silently used the child stops compiling.
- **R8, minification, release build.** Release builds run R8, which shrinks, optimises and renames code, and strip unused resources. They also run a stricter set of lint checks. A debug build passing tells you less than you think about the release build.
- **Lint and `lintVitalRelease`.** Lint is Android's static analyser. `lintVitalRelease` is the subset of fatal lint checks that blocks a release build.
- **Google Play services.** A large closed-source system app from Google, present on most Android devices sold with the Play Store, that provides APIs such as Google Maps and the fused location provider. Apps check for it with `GoogleApiAvailability.isGooglePlayServicesAvailable`, which returns a `ConnectionResult` code. `SUCCESS` is one value; others include "missing", "disabled", "invalid" and "version update required".
- **Fused location provider.** Play services' location API, which combines GPS, Wi-Fi, cell towers and sensors. The alternative is Android's own `LocationManager`, which works without Play services.
- **OpenStreetMap (OSM) and MapLibre.** OpenStreetMap is a free, community-built world map. MapLibre Native is an open-source map renderer that can draw OSM tiles without any Google dependency.
- **APK, signing, Obtainium.** An APK is an installable Android app package; it must be signed. Obtainium is an app that installs and updates apps directly from sources like GitHub Releases, which is how this fork is distributed instead of Google Play.

### Testing terms

- **JVM unit test.** A test that runs on your computer's Java virtual machine, not on Android. Fast, but there is no screen, no real Android framework, and calling many Android APIs simply throws.
- **Instrumented test.** A test that runs on a real device or emulator. Slower, but it exercises the real framework.
- **Robolectric.** A library that simulates much of the Android framework inside the JVM, so you can run many "Android" tests without an emulator.
- **Compose UI test.** A test that renders composables and interacts with them (find the button with this text, click it, assert this node exists).
- **Screenshot test.** A test that renders a screen to an image and compares it pixel by pixel with an approved "golden" image. Tools include Paparazzi and Roborazzi. This is the kind of test that catches "the second hand is missing".
- **Smoke test.** The cheapest possible "does it even turn on" check, such as installing the app and launching it.
- **Characterization test.** A term from Michael Feathers' *Working Effectively with Legacy Code*: a test that records what existing code *actually does*, right or wrong, so you notice when a change alters it. It is the natural tool for migrations.
- **Test oracle.** Whatever tells you the correct answer for a test. For a migration, the oracle is "the old app", but only if you can actually ask it.
- **Regression.** Something that used to work and no longer does.
- **Differential testing.** Running two implementations on the same inputs and flagging differences.
- **N-version programming.** Building the same thing more than once, independently, and comparing the results. A Hacker News commenter proposed it as a cheap AI-era verification technique.
- **Property-based testing and fuzzing.** Generating many random inputs and checking general properties ("never crashes", "round-trips") rather than hand-picked examples.

### AI-assisted coding terms

- **Vibe coding.** Producing software by prompting a model and accepting what comes out with little or no reading of the code.
- **Slop.** Low-quality, mass-produced AI output. "Vibeslop" is the code version.
- **One-shot.** Getting a working result from a single prompt.
- **Agentic loop.** A model that can act (edit files, run commands, read results) and repeat until done, rather than only writing text.
- **Harness.** The software around the model that gives it tools: a chat UI, Claude Code, Codex and so on. The same model in a different harness can have very different abilities, for example whether it can run a compiler.
- **Sandbox.** The restricted computer a hosted model runs commands in. It may not have network access to the places your build needs.
- **Context window and the dump.** The amount of text the model can consider at once. My workflow concatenates the repository into one `dump.txt` (about 800 KB) so it fits in a Claude Project.
- **Effort level.** A setting on current models that controls how much the model reasons before answering. "Medium" is a middle setting.
- **Feature creep.** Scope growing beyond the original goal, one reasonable-sounding addition at a time.
- **Amdahl's law.** If you speed up only part of a job, the overall speedup is limited by the part you did not speed up. Dan Luu uses it to explain why "100x productivity" claims rarely hold for whole projects.

---

## Part 2: What Dan Luu argued

The essay is built around a phrase Michael Malis, the author of pgrust (a Postgres reimplementation in Rust), kept repeating to Dan: we should be more ambitious with the software we build.

The argument, in my words:

- **Most programmers are carrying an outdated cost model.** Things that used to be absurd side quests, such as writing a faster regex engine for your own workload, a Rust interpreter to avoid slow compiles, or a developer toolchain that bypasses the normal filesystem API, can now take hours of human time instead of a year.
- **The slop around you is not a reason to produce slop.** Most AI-generated rewrite projects are garbage, and Dan understands why people dismiss them on sight. But if you test and benchmark properly, the same tools can produce work good enough to use. pgrust's testing turned up real bugs in Postgres itself.
- **Trust is now the scarce thing.** Dan trusts pgrust largely because he knows Malis. He is candid that outsiders have no such shortcut. While he was writing, pgrust got caught up in a ClickBench benchmark controversy, which the Hacker News thread spent a lot of time on.
- **Capability is moving fast.** His Azul board-game AI needed heavy steering with GPT-5.1 and 5.2; a friend later beat it with less effort using Fable 5.1 and Opus 5.5. His toy zstd evaluation became too easy once GPT-6 Astra passed more than 99 per cent of the time.
- **The important caveat, which is the one this post is about.** Dan says newer models do not make his work "just work". He ends up attempting harder things, and he finds that the gap between what a model can implement and what it can verify without hand-holding keeps growing. He quotes Max Bittker's framing that models are excellent at counterfeiting, and that the expertise needed to tell a counterfeit from the real thing keeps rising.

Dan also printed a dissent in his own appendix. Yossi Kreinin argued that most people should be *less* ambitious with AI, because most problems lack clear pass/fail criteria and most people struggle to reason about large black boxes they did not write. He added that "use AI to make things fast" tends to produce code overfit to the case you tested. Dan's reply is that you can often build partially self-checking setups even when people say you cannot, and that fuzzing and property-based testing worked in every domain he was told they would not.

That disagreement, between "you can build the checks" and "most people won't, or can't", is the axis this whole post sits on.

---

## Part 3: What the Hacker News thread added

The thread wandered, as threads do (employment anxiety, CSS on Dan's site, whether Overwatch's render loop is a good model for desktop apps). Under the noise, a few arguments map directly onto my experiment.

**The bottleneck moves; it does not disappear.** The top comment, from `bob1029`, argued that team process (code review latency, coordination, ideology) now dominates, and that faster code writing is wasted if merging and shipping stay slow. `ErrantX` framed it as constraint theory: once building is no longer the slow step, something else becomes the slow step. In a solo project the "something else" is obvious: me, checking.

**Where models succeed.** `Cyph0n` said they had only seen models work well autonomously on tasks that are well specified, well tested, or well represented in training data. Compilers score on all three. Hold onto that list; my migration scored well on only one.

**Existing structure helps a lot.** `jwrallie` suggested that a hand-built 39 per cent of a language gave the model a framework to finish the rest consistently, and `fy20` said their team spent a year and a half making its code base approachable to models. A finished app is a lot of structure. That helped, and it also turned out to be a trap, because structure you cannot see does not help.

**Plausible but wrong.** `majormajor` described Opus 5.5 responding to a reasonable prompt about a swallowed error by injecting a log capturer that string-matched a specific warning from a specific library version and could lose log output. `Karrot_Kream` described Opus 5.5 proposing a hand-written Python load balancer, defending it, and losing to haproxy in a side-by-side test. Both stories share a pattern: the output works in the narrow sense, and the problem is only visible to someone who knows what good looks like. `Supermancho` noted that at their workplace a fix without an explanation of *why* is sent back.

**Reading code versus testing it.** `mlnj` argued that reading every line is no longer feasible and that the time is better spent on a robust test suite. `PostOnce` predicted Knight Capital-scale disasters from unreviewed code. My experience says both are partly right, and that the test suite you write determines which one you get.

**Cheap verification ideas.** `visarga` proposed N-version programming: implement things twice, ideally with different models and languages, and treat disagreement as either a bug or an unclear spec. That is unusually relevant here because, by accident, I did a small version of it.

**After the MVP.** `lubujackson` said AI gets you from zero to a demo quickly but does not remove the long tail of tradeoffs that follow. `phendrenad2` described the experience of thinking you are 20 per cent through a project and discovering you are 5 per cent through, repeatedly. `bubblemoth` complained that vibe-coded projects keep adding features instead of polishing the core. `TehCorwiz` argued that nobody is training models for restraint.

**Is it even ambitious?** `j2kun` and `BrenBarn` argued that making ripgrep faster is not ambition; ambition is about which problems you choose. `weitendorf` pushed the same way: if it does not help a non-technical person, think harder about what you are solving. `superkuh` asked for a different kind of ambition entirely: software that stays stable for fifteen years.

**Trust.** The pgrust ClickBench argument took up a large share of the thread. The specifics do not matter here; the lesson does. When your evidence is "trust me" or "inspect the binary", people will not, and a handful of enormous squashed commits reads as concealment whether or not it is.

---

## Part 4: The experiment

### The starting point

Upstream Positional was started in October 2020 and last pushed in May 2026. It is a real, used app with about 295 GitHub stars. GitHub reports its languages as roughly **70.9 per cent Kotlin, 25 per cent Java and 3.5 per cent HTML** (about 931 KB of Kotlin and 328 KB of Java). Under `src/main/java` there are 321 source files (225 Kotlin, 96 Java), and the repository contains 138 XML layout files.

It had **no automated tests at all**. Not a few: none.

### The goal

Move the UI from fragments and XML views to Jetpack Compose, add tests, update dependencies, and ship from CI. In principle, this is a mechanical translation of an app that already works, using a UI toolkit with excellent documentation and enormous representation in training data.

### The method

- A claude.ai Project, with standing instructions (full files only, no code comments, keep the README current, remove dead code, latest dependencies, battery-friendly native code, never Google Play, target Android 12+ for Obtainium but support older versions where reasonable).
- An `export.sh` script that writes the repository into `docs/llm/dump.txt` so the whole code base fits in the Project's context. It deliberately **skips** drawables, mipmaps, fonts, raw resources, translated `strings.xml` files, HTML assets and Room schema files to stay inside the budget.
- A loop, visible in the commit history: *add prompt*, *add claude response*, *run scripts*. The model delivers a tarball of changed files and a `git rm` list; I extract, commit, push.
- GitHub Actions on every push: unit tests, release lint, a debug build, a Room schema hash check, and on `master` a signed **full release** (not a pre-release) tagged `v1.0.<run number>`. Obtainium users get every one of those.

### The numbers

From the first fork commit (22 October, 23:17 UTC) to the last before this post (24 October, 17:21 UTC) is about **42 hours**. In that time:

- 244 commits: 46 "add prompt", 98 "add claude response", 86 "run scripts", plus a handful of others.
- 22 numbered Claude transcripts and one Gemini transcript, all committed under `docs/llm/vendor/`.
- More than 30 phases (1 to 22, with 8a to 8j, 9a to 9c, 10a to 10c and 14a to 14b).
- **72 pushes to `master` ran CI; 22 of them failed**, roughly 30 per cent. The worst stretch was nine consecutive failures over about 80 minutes on the evening of 23 October.
- The latest release at the time of writing is `v1.0.74`.
- The fork now reports **Kotlin only** (about 622 KB), with 51 JVM test files containing 172 `@Test` methods against 168 main Kotlin files. No AppCompat, Material Components or fragment code remains in the source; nine activities were removed in favour of Navigation Compose destinations.

That is an enormous amount of change for 42 hours. Dan Luu's thesis, at first glance, is vindicated. Now the parts I did not mention last time.

---

## Part 5: What went wrong

### 1. The build broke, a lot, and every fix cost another prompt

Roughly one push in three failed CI. The transcripts and CI annotations show what kinds of failure they were, and the pattern is instructive because almost none of them are deep:

- **A Kotlin name clash.** A shared map base class had a property called `callbacks`. Kotlin automatically generates a setter called `setCallbacks` for it, which collided with a `setCallbacks` function an interface required. Kotlin refuses two methods with the same JVM signature. Three map classes failed to compile.
- **A vanished transitive dependency.** Phase 8i removed AppCompat and Material Components. Two Java helpers had been using `LinearOutSlowInInterpolator` from `androidx.interpolator`, which only arrived transitively through those libraries. Once the parents were gone, `package androidx.interpolator.view.animation does not exist`. The fix was the platform `PathInterpolator(0f, 0f, 0.2f, 1f)`, which draws the same curve.
- **Nullability meeting reality.** The Room schema allows trails without a name, so the converted Kotlin entity had to make the name nullable, and three call sites still assumed it was always set.
- **A JVM test that called Android.** `PanelSettingsTest.unknownPinSkinFallsBackToDefault` failed (156 tests ran, 1 failed) because an object built its colours with `Color.parseColor`, an Android API that throws in a plain JVM test. The fix was to make the colours constants.
- **A release-only failure.** Phase 6 removed the English `show_on_map` string but left it in 13 translation files. The debug build used by the test job did not run the check that flags that; the release build did, so tests went green and the release job went red. The workflow was then changed to run `lintVitalRelease` in the test job, which is why it appears there today.
- **A Dependabot pull request** failed at `checkDebugAarMetadata` because a new AndroidX release wanted a newer `compileSdk`.

The single most important sentence in all 22 transcripts is this one, from the model, in my own repository: *"Done" in my earlier replies only meant the code was written.* It went on to say that none of it had been compiled or run, because the sandbox could not reach Google's Maven repository or the Android SDK.

That is not a model limitation in the usual sense. It is a harness limitation. The claude.ai sandbox I was using could not download the Android Gradle Plugin or AndroidX libraries, so the model had to write Android code blind and treat my CI as its compiler, with me as the network cable between them. Each compile error meant: I read the CI log, paste it in, the model fixes it, I extract, commit, push, wait. A 30-second compile loop for an agent with an SDK became a 5-to-20-minute loop with a human in the middle.

Worse, at one point four phases (9c, 10a, 10b and 10c, 64 files) were written before any of them had been compiled. The model itself said, in effect, let's not stack a fifth untested phase on top. (That same message also revealed that it had written several tarballs without attaching them, so I could not have downloaded them anyway.) Stacking untested changes is how a one-line error becomes a nine-run failure streak: when the build breaks, you no longer know which of 64 files broke it.

### 2. The build was green and the app would not launch

Passing CI does not mean a working app. After phase 7, the build passed, the APK was produced and signed, it installed, and it crashed immediately on launch. Every time.

The cause, from the transcript: the new Compose clock drew its sweeping "trail" image through `painterResource`. That drawable was an XML `<shape>`, which the View system supports and Compose does not. It threw as soon as the Clock panel was drawn. Because the app reopens on the last panel you used, once you had looked at the clock, **every subsequent launch crashed before you could navigate away**. The time zone picker had the same defect with a `<layer-list>` dot, which would have crashed whenever a selected zone was on screen.

Why did nothing catch this?

- **The unit tests run on the JVM and never render anything.** They test logic, and this was a rendering-time failure.
- **There was no launch test.** CI never started the app.
- **The model could not see the drawable.** `export.sh` excludes `res/drawable*` to save context. The model was asked to convert code that referenced a file it had literally never been shown, and guessed that it was a vector. A human porting the same screen would have opened the file.

After the crash, the model added `ComposeDrawablesTest`, which fails the build if any drawable used from Compose is not a vector or bitmap. That is a good test. It is also a test that exists because a user (me) hit the bug, which is the wrong order. Notice too that the crash fix was delivered in the same archive as the next phase of new work (8a), which made the change harder to isolate.

### 3. I cannot find the regressions I do not know about

This is the uncomfortable one. I did not write Positional and I have not used it daily for years. If I spent a hundred years meticulously using the migrated app, I would still miss regressions, because **you can only notice a change from behaviour you already know**.

In testing terms, the migration had no usable oracle. The ideal oracle for a migration is "the old app", but:

- Upstream had zero tests, so there was no executable description of its behaviour.
- I did not record the old app's behaviour before changing it: no screenshots, no screen recordings, no list of what each panel shows, no notes on gestures, menus, dialogs or settings.
- The 172 new tests were written by the same model, at the same time, from the same understanding of the code. They encode what it thought the app does, not what the app did. That is circular.

The tests I have mostly protect the new code from future changes. They do very little to prove the new code matches the old one.

### 4. The feature I added to make testing easier is the one that still fails

Midway through, the scope grew from "migrate to Compose" to "also let every map use OpenStreetMap through MapLibre". The reason was practical: I wanted to test on my Samsung Galaxy Tab A9+, and Google Maps there was giving me trouble.

The cruel irony: I installed the latest release on that tablet while writing this, opened the map screen, and got the dialog *"Your device does not support required Google Play dependencies."* The feature meant to help me test was itself never properly tested on the device it was built for.

Reading the current code explains a lot:

- The map source setting has three choices: Automatic, Google Maps and OpenStreetMap. **Automatic chooses OpenStreetMap only when the build has no Google Maps API key.** It does not ask whether Google Play services actually works on *this device*. A release built with a key will choose Google on every device, including ones where Play services is missing or unusable.
- The GPS panel shows the error when it "needs" Play services, defined as *the map source is not OpenStreetMap, or the location provider is the fused one*. The default location provider is Android's own, so on a fresh install the deciding factor is the map source.
- The check lumps every non-success result into one message. `isGooglePlayServicesAvailable` distinguishes "missing" from "disabled", "invalid", "updating" and "version update required", and Google provides a standard dialog for the user-fixable ones. The app shows none of that. "Does not support" may simply be wrong; it could just as easily be "needs an update" or "is disabled". As far as I know the Galaxy Tab A9+ is sold with Google Play, so a version or state problem is at least as plausible as a missing one, but I have not yet logged the actual result code, and that is the first thing to do.
- `MapSourceTest` checks exactly the rule that was written ("automatic falls back to OSM without a key") and therefore cannot catch the rule being the wrong rule. It is a clean example of a test sharing its author's blind spot.

A sketch of the kind of change that would address the logic (untested, and written to show the idea, not as a patch):

```kotlin
fun useOsm(choice: Int, hasGoogleKey: Boolean, hasPlayServices: Boolean) =
    choice == OSM || (choice != GOOGLE && (!hasGoogleKey || !hasPlayServices))
```

```kotlin
val api = GoogleApiAvailability.getInstance()
val code = api.isGooglePlayServicesAvailable(activity)
if (code != ConnectionResult.SUCCESS && api.isUserResolvableError(code)) {
    api.getErrorDialog(activity, code, PLAY_SERVICES_REQUEST)?.show()
}
```

The deeper lesson is about scope. `bubblemoth` on Hacker News complained about vibe-coded projects that keep adding features instead of polishing the original concept. This was a defensible addition with a clear motive, and it still enlarged the surface I had to verify at the exact moment I was failing to verify the surface I already had.

### 5. The second hand disappeared, and nobody noticed for a long time

Phase 7 rebuilt the clock panel in Compose. The hour and minute hands drew correctly. The second hand did not appear at all. It survived through phase 8a, 8b, all the way to 13, until I noticed it myself and asked for it back in the session that became phase 14a.

The cause was one word. The second needle was sized with `Modifier.height(...)` instead of `Modifier.size(...)`. Its width therefore fell back to the vector's natural size, about 24 dp, so the hand was drawn tiny in the centre of the face, hidden under the hub. The needle picker dialog used `size`, which is why the second hand looked fine there and made the bug even harder to spot. The same mistake had shrunk the empty trail picture.

A screenshot test of the clock panel would have caught this the first time it ran. A Compose UI test asserting that the second-hand node has the same bounds as the minute hand would have caught it. Neither existed. And this is the visible bug. Item 3 says there are probably invisible ones.

It is worth noting *when* it was fixed. Just before it, I had pasted in an independent review of the migration from a different model (Gemini), with a note to use judgement because it might be out of date. That review flagged real structural problems: dialogs stored as composable lambdas rather than as state, a search result callback held in an activity field that would be lost on rotation or process death, panels that rebuild from scratch on every switch, and imperative `LiveData` bridging. Several of the phases that followed respond to it: 14a turned dialogs into state and moved the search result into a `SavedStateHandle`, and 14b to 16 moved panel state and settings into view models. The second-hand fix rode along in 14a, in the same session where I asked for every defect and regression to be fixed. A second pair of eyes, even an artificial one, changed the trajectory of the project. I did that once. I should have done it at every phase.

### 6. "This should be easy." Was it a skill issue?

The honest answer is: **partly yes, and mostly not in the way the phrase usually means.**

First, I want to push back on my own premise. This was not a small change. The upstream app had 321 source files, 96 of them Java, 138 layouts, fragments, a view pager, popup windows, services, a local broadcast bus, `LiveData`, three Room databases that real users have on their phones, and **zero tests**. And then I did not ask for "fragments to Compose". I asked, through standing instructions, for fragments to Compose *and* Java to Kotlin *and* `LiveData` to `StateFlow` *and* services to lifecycle-bound flows *and* widgets to Glance *and* the latest version of every dependency *and* no dead code *and* battery improvements, and then added a second map engine. Each item is reasonable. Together they are a rewrite of nearly every file. Phendrenad2's "you think you are 20 per cent done and you are 5 per cent done" applies.

Second, the model's *writing* was mostly good. Many phases compiled on the first try, and when they did not, the errors were small and were fixed in one round. The explanations of root causes in the transcripts are clear and, as far as I can tell, correct. A higher effort level might have caught a few more issues by reasoning, but no amount of reasoning lets a model see a drawable it was never shown or compile against a library it cannot download.

So what failed was the *system*, and I designed the system. Specifically:

- **I gave the model no way to check its own work.** No SDK, no Gradle, no emulator, no device. The verification gap Dan Luu describes was not a property of the model here; it was a property of the harness I chose. The model said so repeatedly.
- **I did not capture the old behaviour before changing it.** No characterization tests, no golden screenshots, no written feature inventory. Without an oracle, the migration could only be checked against my memory of an app I barely knew.
- **My context-saving rules created blind spots that mapped exactly onto the bugs.** Excluding drawables produced the launch crash. Excluding translations produced the release-only failure. The dump was optimised for fitting into context, not for containing what the code depends on.
- **I let unverified work pile up.** Four phases in a row without a compile. A crash fix bundled with a new feature phase.
- **I treated "CI is green" as "it works".** CI never launched the app, never ran a release-like build in the test job until a release broke, never rendered a screen, and never ran on a tablet.
- **I let scope grow during the migration.** The OpenStreetMap work should have been a separate project after the migration was verified, not a mid-migration addition.
- **I published every push as a full release.** That is great for velocity and terrible for anyone who installed it through Obtainium while the app could not launch.

In Cyph0n's three-bucket terms, the migration was **well represented in training data** (Compose is everywhere), **not well specified** ("make it the same, but in Compose" is a spec only if someone writes down what "the same" means), and **not well tested** (zero tests to start, and the new ones were written by the author of the change). One out of three. That is roughly the success rate I got.

---

## Part 6: What could have been done differently

Here is what I would do if I started this again tomorrow, roughly in order of payoff.

### Before writing any new code

1. **Write down what "done" means.** For a migration: every screen, dialog, menu, gesture, setting and widget behaves as before, the database opens unchanged, preference keys are preserved, placed widgets keep working, and the app launches on every supported device class (phone, tablet, with and without Google Play services). If a model is writing the code, it should be told this explicitly, and its answer to "done" should be "compiled, tested, launched", not "written".
2. **Record the old app.** Screen recordings of every panel and dialog, on a phone and on the tablet. Screenshots of each screen in light and dark mode. This costs an afternoon and gives you, and the model, an oracle.
3. **Add characterization tests to the old code first.** Before migrating a panel, capture its current behaviour in screenshot tests (Paparazzi or Roborazzi) and in tests of its logic. Then migrate, and require the same tests to pass. A missing second hand fails a screenshot comparison on day one.
4. **Make the context match the dependencies.** If code references a drawable, the model needs to see that drawable, or at least a listing of each drawable's root element type. Small XML drawables cost very little context; it was the large launcher art vectors that needed excluding, not the `<shape>` files.

### Give the agent a real loop

5. **Use a harness that can build Android.** Claude Code on my own Fedora machine, with the Android SDK, Gradle cache and an emulator or a USB-connected phone, closes the loop the claude.ai sandbox could not. The agent can compile, run unit tests, install the APK, launch it, and read `logcat` itself. Most of the 22 failed CI runs would have become 30-second local iterations the model handled without me.
6. **Add a launch smoke test to CI.** On GitHub Actions, an emulator job (for example with `reactivecircus/android-emulator-runner`) can install the APK, start the main activity, open each panel in turn, and fail if `logcat` shows a fatal exception. Running Android's `monkey` tool for a minute of random taps is a crude but effective extra. This turns "the app cannot launch" from something I discovered by hand into a red build.
7. **Test the build you ship.** Run lint and the smoke test against a release-configured, minified build, because that is what users install.
8. **Put the tablet in the matrix.** At minimum: a phone emulator image with Google Play services, one without, and a large-screen (tablet) configuration. The "does not support Google Play dependencies" dialog is exactly what an emulator image without Play services would have shown in CI.

### Change the process

9. **One phase at a time, green before the next.** Never stack untested phases. Never bundle a bug fix with new work.
10. **Separate scope.** Migrate first, verify, release. Then add OpenStreetMap as its own phase, with its own tests on the devices it exists for.
11. **Ask for the unverified list.** End every phase with the question: what did you change that no test exercises, and what could you not check? The model was candid about this when asked; I should have made it a standing rule.
12. **Use a second model as a reviewer at every phase, not once.** The single Gemini review produced several phases of meaningful fixes. This is a cheap version of N-version programming: a different model with different blind spots reviews the diff against the original behaviour.
13. **Ship to a pre-release channel first.** Publish pre-releases from `master` and promote to a full release only after a smoke test on real devices. Obtainium can track either.
14. **Make errors diagnosable.** Log the actual `ConnectionResult` code. Show Google's resolution dialog when the error is user-fixable. A generic message turns a five-minute fix into a mystery.

### Measure the outcome

15. **Measure what the migration was for.** Cold-start time, APK size, battery use over an hour of trail recording, crash-free sessions. Without numbers, "it's better" is a feeling. The repository has some of these as intentions (phase 21 shrank launcher art XML from about 16.2 MB to 10.4 MB; phase 22 rasterises art off the main thread), but nothing compares against the last upstream release.

---

## Part 7: What people can learn from this

### From the experiment

- **"It compiles" and "it works" are different claims, and "the model said done" is neither.** Make the model's definition of done match yours, and make the harness capable of checking it.
- **The cheapest bug to find is in behaviour you recorded.** Characterization tests and screenshots of the old system are the highest-value hour of any migration.
- **A test suite written by the author of the change verifies the author's beliefs.** That is useful for catching future breakage and useless for catching today's misunderstanding.
- **Context budgets create blind spots, and blind spots become bugs.** Decide what to leave out by asking what the code depends on, not what is large.
- **Feature creep is most dangerous during verification debt.** Every new feature is new surface you also have to check.
- **Human device testing found the worst bugs.** The launch crash, the missing second hand, the overlapping headings on a OnePlus Nord N30 5G with a large font size, and now the tablet's Play services dialog were all found by a person holding a device. None were found by the 172 tests. That is a sign the tests are aimed at the wrong layer, not that testing does not work.

### From Dan Luu's essay

Dan was right that the cost model has changed. Forty-two hours for a migration of this size, including a Java-to-Kotlin conversion, would have been unthinkable for me a year ago. But the part of his essay that matters most is the part that is easiest to skim: newer models make him attempt harder things, and the distance between what a model can build and what it can verify without help keeps growing. My experiment is a small, concrete instance of that sentence. The model could build; it could not verify; I was the verifier, and I was not equipped for it.

His rebuttal to Yossi Kreinin also holds up: you usually *can* build self-checking setups. The schema hash gate, the release lint check, the drawable-type test and the unused-resource gate in this repository are all examples, and every one of them was added after the failure it prevents. Dan's advice works better if you take it before the first bug instead of after each one.

### From Yossi Kreinin's appendix

Yossi said that without clear pass/fail criteria, and without the ability to reason about a black box you did not write, ambition with AI ends in failure for most people. For the parts of this project that had pass/fail criteria (compile, schema hash, lint, unit tests), progress was quick and failures were fixed quickly. For the parts that did not (does the clock look right, does the map work on this tablet, is anything missing that I would not recognise), progress was slow and failures lasted for days. That is his argument, measured.

### From the Hacker News thread

- `ErrantX` and `bob1029` were right that the bottleneck moves. In a solo project it moves to the one human who has to check everything.
- `Cyph0n`'s three buckets are a good pre-flight checklist: if a task is only "well represented", fix the other two before starting.
- `visarga`'s N-version idea works and is cheap; I just did it once instead of always.
- `mlnj` and `PostOnce` were both right: tests can replace reading, but only tests aimed at the right layer, and without them you get a Knight Capital moment in miniature (an app that cannot launch, shipped to every Obtainium user).
- `bubblemoth` and `TehCorwiz` were right about restraint. The model never once suggested deferring the OpenStreetMap work until the migration was verified. Restraint was my job.
- `j2kun` and `BrenBarn` asked what ambition is *for*. A migration is internal plumbing. It only counts as ambitious if users end up with something measurably better: faster, lighter, longer battery life, more devices supported. I have not shown that yet.

---

## Part 8: Where this leaves me

Was this a skill issue? Yes, if "skill" means designing the verification around the model rather than prompting it better. The prompts were fine. The code was mostly fine. What was missing was an oracle, a loop, and the discipline to keep the scope small until both existed.

Dan Luu's essay is usually read as permission to build more. My experiment suggests it is just as much a warning: the more you can build, the more you have to know, and knowing is the part that did not get cheaper. If you are about to attempt something similar, spend the first day not on code but on deciding how you will find out whether the code is right. Then give your model the same ability.

The next phases of this project are the boring ones I should have done first: record the upstream app on a phone and a tablet, add screenshot tests for every panel, add an emulator launch test with and without Google Play services, log the actual Play services result on the Galaxy Tab A9+, fix the Automatic map source to account for it, and move the work to a harness where the model can run what it writes.

---

## Sources and links

- Dan Luu, [We should be more ambitious with the software we build](https://danluu.com/ambitious/)
- [Hacker News discussion of the essay](https://news.ycombinator.com/item?id=50225695) (about 458 points and 426 comments when read; comments paraphrased here and attributed by username)
- [Hacker News thread on the pgrust ClickBench controversy](https://news.ycombinator.com/item?id=50205816)
- The experiment: [github.com/kusl/hamza417-positional](https://github.com/kusl/hamza417-positional), including the prompts, model responses and phase archives under `docs/llm/vendor/`
- The upstream app: [github.com/Hamza417/Positional](https://github.com/Hamza417/Positional)
- The earlier post on this site: [Ambition, Verification and a Finished Android App](https://kush.runasp.net/post/ambition-verification-and-a-finished-android-app-what-dan-luu-hacker-news-and-a-fragments-to-compose-migration-say-about-each-other)

*Repository figures (commit counts, CI run results, file counts, language statistics) were read from the public GitHub API and the repository dump on 25 October 2026 and will change as the project continues. Root causes of individual failures are taken from the committed model transcripts and CI annotations. The analysis of the Google Play services dialog is based on reading the current code; the exact result code on the tablet has not yet been logged.*
