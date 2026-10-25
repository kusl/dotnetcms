Ambition, Verification and a Finished Android App: What Dan Luu, Hacker News and a Fragments-to-Compose Migration Say About Each Other

Dan Luu argued that cheap agentic coding should make us attempt work we used to skip; Hacker News pushed back on process, verification, trust and what "ambitious" even means. This article tests both against a real experiment: migrating the finished Android app Positional from fragments, Views and Java to Jetpack Compose with Claude over more than 20 phases.

---

## The short version

On 25 October 2026, Dan Luu's post "We should be more ambitious with the software we build" was on the front page of Hacker News, with several hundred points and roughly 400 comments. His claim is simple: the cost model most programmers carry around is out of date, so a lot of work that nobody would have attempted (a faster regex engine, a Rust interpreter, a formally verified database port, a hyper-personalised editor function) is now cheap enough to just do.

The comment thread did what Hacker News does. It agreed, disagreed, drifted into employment anxiety, argued about CSS, and spent a long time on a benchmark controversy involving the project Dan used as his lead example. But underneath the noise, a handful of questions kept coming back:

1. Where does the bottleneck move once writing code is cheap?
2. How do you know the code is right when you did not read it?
3. Is rewriting existing things "ambitious", or just fast?
4. Whom should you trust when everyone's repository looks equally polished?

Separately, someone ran an experiment: take an existing, fairly new, mostly finished Android app, [`Hamza417/Positional`](https://github.com/Hamza417/Positional), and have an LLM migrate its UI from fragments to Kotlin Jetpack Compose. The result lives at [`kusl/hamza417-positional`](https://github.com/kusl/hamza417-positional). It is a good test case, because it is exactly the kind of work the essay and the thread disagree about: a large, unglamorous, internally motivated change to software that already works.

The experiment turns out to support Dan Luu's thesis in some ways and Yossi Kreinin's counter-thesis (the essay's own appendix) in others. The rest of this article goes through the evidence.

---

## Part 1: What Dan Luu actually argued

### The trigger

The post starts from a recurring remark by Michael Malis, the author of pgrust (a Postgres reimplementation in Rust): we should be more ambitious with the software we build. Dan notes that he would normally assume a project like pgrust is vibe-coded slop, as most rewrite projects are, and that he trusted it only because he knows Malis and because Malis's testing had turned up real upstream Postgres bugs (numeric, JSONPath caching, pgcrypto, dictionary loader and money-overflow issues, though a Postgres contributor disputed whether some of these count as bugs).

### The core argument

Dan's argument has a few layers:

- **A pre-agentic cost model is the real obstacle.** People still treat "write a new compiler because mine is slow" the way they treated it ten years ago, as a symptom of someone who never ships. That instinct is now wrong more often than right.
- **"Ambitious" is deliberately broad.** He uses it for both bigger projects than were feasible before, and for super-specialised tools that were always possible but never worth the time. He argues both fall out of the same cause: producing software got much easier.
- **Personal evidence.** Dan did not know regex internals, compilers or interpreters, yet in a low single-digit number of hours he got a workload-tunable regex engine, a ripgrep fork using its native-code backend, and a Rust interpreter-style build accelerator (a large cold-build speedup, a smaller warm one, held back by not wanting to fork the Rust front end). He also moved from Mac to Linux for syscall and filesystem overhead and argues that, with modern disks, you could build developer tooling around direct I/O instead of the standard *nix filesystem interface.
- **Capability jumps are real.** His Azul board-game AI took about 5 hours to become superhuman and about 20 to become, in his judgement, the strongest available. A friend then built a much stronger one in less time, partly because newer models understand board-game AI far better and can read a paper and implement its techniques correctly. A zstd implementation eval that was once discriminating became saturated, so he moved to harder targets (an IMAP implementation, Pandoc).
- **"It just works" is still false.** This is the most interesting claim. As models get better, he works on harder things, and so he lands *further* from "it just works" with each release, because the gap between what a model can implement and what it can verify without hand-holding keeps widening.
- **Skills are not obsolete yet.** In a footnote he argues programming skill is currently higher-leverage than before, and cites a framing that models are good counterfeiters, so the expertise needed to tell the counterfeit from the real thing grows as models improve.

### The appendix that argues the opposite

Dan includes a comment from Yossi Kreinin, who says most people should be *less* ambitious with AI, or they will fail. Yossi's points: many problems have no clean pass/fail criteria for an agent loop; people struggle to reason about large black boxes whose internals they cannot follow; and "software should not be slow" pushed by a manager onto people who cannot decompose the system leads to messy, overfit optimisations that neither generalise nor survive later changes.

Dan's reply is not to dismiss this but to say the answer is to build **self-checking setups**: consistency checks across contradictory rule descriptions (his Guards of Atlantis 2 work), fuzzing and property-based testing, differential checks against a reference. He observes that every domain people told him such testing would not work in turned out to be fine.

### Two things he is explicit about

- The post was written fast, with minimal review, and says so. Without rigorous benchmarking for the projects mentioned, treat the numbers as "works for me".
- He expects much of his own tooling to be obsoleted by AI labs soon, so he deliberately avoids over-investing in any single tool. He is also sceptical of agent-orchestration frameworks that seem to mostly build themselves.

---

## Part 2: What Hacker News made of it

The thread is long and tangled. Rather than summarise comment by comment, here are the clusters that matter.

### Cluster A: the bottleneck moves to process, review and decisions

The top comment argued that team process, not code writing, is now the constraint: PR review latency, merge conflicts on stacked PRs, and the coordination tax. Others extended it. One commenter used constraint theory: if build time shrinks tenfold, business ideation, governance and prioritisation must speed up too. Another said deciding what to build becomes the bottleneck, and that the risk is building the wrong thing faster. A reply pointed to a published Anthropic approach to asynchronous code review as evidence that review can be decoupled rather than removed.

There was also a pushback chain about backlogs and layoffs: if a team burns the backlog to zero, does management hire more work or fewer people? Opinions split sharply, and the exchange got heated.

### Cluster B: verification, tests and "should anyone read the code?"

This was the most technically useful strand:

- One view: reading generated lines is no longer feasible and a robust test suite is the better use of developer time. A sharper version predicted code would not be reviewed by humans at all, and others compared eyeballing machine output to hand-checking compiler address arithmetic.
- The counter: the examples people gave of "fine but wrong" output. One described an agent that, asked to surface a swallowed error, hooked a log stream and string-matched a specific warning from a third-party library version, with a buffering side effect that could lose messages. Another described an agent that proposed a Python-level load balancer where HAProxy was clearly right; the throughput improved and it still was not best practice.
- A commenter listed three conditions under which LLMs do well autonomously: well-specified, well-tested (a form of spec), and well-represented in training data. Compilers, interpreters and ports tick all three.
- Another proposed N-version programming: have multiple independent implementations in different languages and harnesses, and treat disagreements as bugs in either one implementation or the spec.
- Someone noted that "rewrite X in Y" is an ideal reinforcement-learning task because X is a free oracle. A sub-thread argued about whether "it is in the training data" is still a meaningful critique, with one side saying it is a stale "stochastic parrot" argument and the other pointing out that the existing implementation effectively acts as the specification.

### Cluster C: is this really "ambitious"?

Several commenters felt the examples were underwhelming: a faster ripgrep for one person's workload is useful, but is it ambitious? Others replied that ambition scales with the number of previously impractical small things you can now do. The sharpest framing distinguished *technical* ambition (a better mousetrap) from *impact* ambition (eradicating the plague with it), and noted that most examples were developer tooling for developers. A related critique: agents building tools for agent users is a closed loop, and someone should use these tools to solve problems for non-technical people (venues' websites, municipal signup forms).

Another strand observed that ambition is also restraint. Elegance is what you leave out, models were not trained for restraint, and many vibe-coded projects keep bolting on features without polishing the original idea.

### Cluster D: trust, and the pgrust benchmark controversy

Dan's post says he trusts Malis personally and treats the "cheating on benchmarks" accusations as false. The thread had just days earlier hosted a controversy about pgrust's preview build being removed from ClickBench. Commenters pointed out the awkwardness of using a personal-trust argument while the project was in that dispute. Malis answered in the thread that the submitted binary can be inspected, that code paths can be logged and instrumented, and that the hand-rolled code in the binary was for testing, not used in the benchmark run. Critics replied that "audit the binary" is not satisfying when the public repository was reported as containing a commit touching hundreds of thousands of files, and that releasing the commit history would settle more than assurances would. Dan's own footnote says the accusation of deliberate cheating looks false but that submitting a preview too early was acknowledged.

For the purposes of this article, the point is not who is right. It is that in a world where polished code is cheap, **provenance and transparency become the scarce signal.** Hold that thought for the experiment below.

### Cluster E: the human angle

A large side-thread was about jobs, status and anxiety. Some argued the comfortable profession now faces what other occupations faced earlier; others said that telling people to simply adapt is tone-deaf when many have little savings. At one point a commenter disclosed that they were in a very dark place after a layoff, other community members responded with care, and the commenter said they were already seeing a counsellor and were going to see a doctor. It is a reminder that the abstract debate about ambition sits on top of real livelihoods.

### Cluster F: personal projects and counter-examples

The thread was full of "here is what I built": a from-scratch JavaScript engine (and an argument about whether "from scratch" is honest when engines are in the training data), a robust apt cache replacing a hand-maintained one, a Rust UI harness that avoids Electron, a Kobo SDK, an accounting and POS suite for Norwegian businesses, a C++ compiler reported to compile several times faster than GCC, a game remake, a spreadsheet and a Logseq-compatible outliner whose author said most time went into tests (byte-for-byte round-tripping of real graphs, automated UI tests on a simulated phone). Two threads recur in these: the existing codebase as a framework for the model to continue (one developer reported an LLM finishing a language in a single pass after eight weeks of hand-built groundwork), and the importance of making a codebase approachable to the model through conventions and tooling.

### Cluster G: sidebars

There were also tangents on the author's deliberately plain site (defended by many as readable and fast, criticised by some), on energy use, on whether Claude Code and Codex are really just new IDEs, on UBI versus Soviet-style planning, and on whether software should be more stable for decades rather than churn faster. They are not covered further here.

---

## Part 3: The experiment

### What was done

[`kusl/hamza417-positional`](https://github.com/kusl/hamza417-positional) is a community fork of Hamza417's Positional, a Kotlin Android app (compass, GPS, clock with sun and moon data, level, trails, measurements and home-screen widgets). The upstream project dates from October 2020 and is an ordinary, successful small app: roughly 295 stars on GitHub, and a code base GitHub measures at about 930 KB of Kotlin plus about 330 KB of Java.

The fork was created on 22 October 2026. By the 24th, according to its README and commit history:

- Its UI was in Jetpack Compose (Material 3), hosted by `ComponentActivity` subclasses, with **no AppCompat, Material Components or fragments** left.
- Panels (GPS, compass, clock, level, trails, measure, direction) became plain `PanelController` classes with their own child lifecycle, so that sensors, maps, location observers and coroutines stop when a panel is not shown.
- Sub-screens became Navigation Compose destinations; nine activities were removed.
- View models moved from `LiveData` to `StateFlow`; the `lifecycle-livedata` dependency was removed.
- Widgets moved to Glance for sun, moon and twilight, and to the platform analog clock widget on Android 12+ so the launcher ticks the hands with no background service.
- Location providers became lifecycle-bound cold flows, replacing started services and a local broadcast bus; fused updates dropped from every 100 ms to every second.
- All Java was converted to Kotlin. GitHub's language statistics now show Kotlin only (about 620 KB), though raw byte counts are not a like-for-like comparison, since dead code and resources were deleted at the same time and tests were added.
- A map-source setting was added so every map can be Google Maps or OpenStreetMap via MapLibre.
- Play Store licensing, analytics, crash reporting and upstream social links were removed, and legal pages were rewritten for the fork.

The README lists the work as phases (1, 2, 3 … 8a to 8j, 9a to 9c, 10a to 10c, 11 to 15), and the archives in the repository show at least 21 phases. Each phase is a short, scoped change with a stated purpose, for example "panels as plain controller classes; fragments and the fragment host removed" (8h) or "Phase 18: a cancellable delayed-task helper replaces `Handler` callbacks in the map views".

### How it was done

The workflow is visible in the repository, which is itself unusual:

- `docs/llm/` holds `instructions.md` (the standing brief), numbered transcripts of the Claude sessions (`1.txt` to `22.txt`), around 30 phase archives (`.tar.gz`) delivered by the model, and a `dump.txt` of about 800 KB.
- `export.sh` regenerates `dump.txt`: a tree and the contents of the tracked files minus assets, translations and other bulk. It exists so the **whole code base fits inside a Claude project's context** instead of spilling into retrieval mode. That constraint shapes everything else.
- The commit log shows a repeating cycle: *add prompt → add claude response → run scripts*, many times a day.
- The standing instructions are opinionated: no code comments; concise output; when only a few files change, return the complete file with its full path; when many change, return a whole-project tarball with unambiguous placement and an explicit `git rm` list; no sed or patch scripts; keep the README current; remove unused code; prefer native, battery-friendly code; support old Android versions where reasonable but target Android 12+ for Obtainium auto-updates; never Google Play; use the latest dependencies and GitHub Actions.
- CI builds, runs the tests and publishes a **full release on every push to `master`**; three release tags landed on 24 October alone (v1.0.72 to v1.0.74).

### Safety nets that were added along the way

Because the model's sandbox had no Android SDK (a transcript says explicitly that CI would be the first compile and test run), the project invested in machine-checkable gates rather than trust:

- A large and growing unit-test suite. In the repository there are 51 Kotlin test files against 168 main Kotlin files, covering state holders, catalogs, coordinate maths, sun and moon refresh distance, map animation interpolation, trail naming rules, widget day and night logic, needle physics and more. Timer tests use virtual time via coroutines test support instead of real waits.
- Lint configured so that **an unused resource fails the release build** (phase 20), which turns "remove dead code" from a request into an enforced invariant.
- A Room check so that **CI fails if an exported schema hash changes**, which protects existing users' databases through the migration.
- A test that every drawable drawn by Compose is a vector or bitmap, not an unsupported type.

Phase 21 then targeted APK size and load time by rounding the coordinates in large splash-art vector drawables. The transcripts also show the cost side: weekly-usage meters in the pasted transcripts reading in the low 80 per cent range late in the sequence.

---

## Part 4: Reading the experiment through the essay and the thread

### Where it supports Dan Luu

**It is the "work we would never have scheduled" category.** A finished app with a working UI is the classic case where a Compose migration would sit on a backlog forever: large, mechanical, internally motivated, no user-visible feature. Dan's central observation, that people reject it because of a pre-agentic cost model, fits. The migration plus a cleanup pass (Java to Kotlin, `LiveData` to `StateFlow`, services to flows, deprecated APIs replaced, dead resources deleted) would be a multi-month project for a solo maintainer. Here the visible history compresses it into days.

**It rewards depth, not just speed.** Several changes are not mere translations. The work includes sensors that register only while resumed and at game rate rather than fastest, needles that animate only until they settle, a clock that pauses with the frame clock in the background, address lookups only after moving 25 m, sun and moon reports recomputed once a minute or after moving a kilometre, and a last-known position written once after 50 m instead of three writes per fix. That is the kind of battery and correctness polish that rarely survives a deadline. The standing brief ("try to not destroy the user's battery") turned a migration into a performance review, which is what "ambition" looks like when cost is no longer the filter.

**It is the HN "existing codebase as scaffold" pattern.** A commenter described an LLM finishing a hand-started language in a single pass, and another said their team had invested in making the code base approachable to models. Positional has the same shape: a mature app is a rich specification, a large body of examples of intended behaviour, and conventions to follow. In the three-condition framing from the thread (well specified, well tested, well represented in training data), a UI-framework migration scores unusually well on the third, because Compose, `StateFlow`, Navigation and Glance are all heavily documented.

**It follows Dan's advice on self-checking systems.** Dan's rebuttal to Yossi is to build checks the human can set up cheaply. The lint rule, the schema-hash gate, the virtual-time tests and the full CI-on-every-push pipeline are that advice made concrete.

### Where it supports Yossi Kreinin and the sceptics

**The verification gap is visible in the repo.** The model could not build the project. Every phase therefore ended with a handoff: extract the tarball, run the git removal, push, wait for CI, and sometimes install on a device and look (a transcript tells the maintainer to check that Settings still shows the version on their phone). That is Dan's "gap between what the model can implement and what it can verify" in miniature. The human, plus CI, was the verifier, and the model was optimistic enough to add "if CI reports something my check missed, paste the lint report" at the end of a phase.

**Scope grew, which is the failure mode Yossi describes.** The task, as stated, was fragments to Compose. The repository now also contains an OpenStreetMap and MapLibre map option, a second location-provider abstraction, new widget implementations, Java-to-Kotlin conversion, ABI filtering and release automation. Each step is individually defensible and documented, but the aggregate is a different, larger project than the one that began. A cautious maintainer should ask whether each addition earns its maintenance cost. Several HN commenters made the same point about vibe-coded projects that keep growing without polishing the core.

**Review is where it is thinnest.** The HN thread's core disagreement is whether anyone needs to read the output. This project mostly takes the "tests and CI are the review" side, which is defensible given 51 test files and strict lint gates, but UI correctness (layout, gesture behaviour, sensor feel, widget rendering on real launchers) is the category tests catch worst. A compass app that is wrong by a few degrees, or a widget that stops ticking on one vendor's launcher, will not fail a unit test. Those checks depend on someone carrying the phone around.

**Documentation drift is a risk.** The standing instruction is to keep the README up to date at every change. The README is now around 20 KB, with a very long architecture paragraph and an equally long "differences from upstream" bullet. It is accurate as a changelog and heavy as documentation. A future contributor, or a future model, will have to read it all.

### Where the thread's trust question applies

Dan trusted pgrust because he knows Malis; commenters argued that is a weak basis for everyone else, especially when the offered evidence was "inspect the binary" and the visible commit history was a handful of enormous commits. This repository makes the opposite choice: **the prompts, responses and delivered archives are all committed**, in order, alongside the code. That is not the same as proving the code is correct, but it is unusually good provenance. A reviewer can see what was asked, what was claimed, and what changed. In a world where polished code is free, "show your process" may be the cheapest credible signal available to a solo maintainer.

It does have a cost. Binary archives and large transcripts bloat the repository (GitHub reports roughly 145 MB, with a single archive of about 9 MB), and the transcripts include internal chatter that would not normally be published. Whether the trade is worth it depends on the maintainer's priorities.

### Where it answers the "is it ambitious?" argument

HN's sharpest critique was that making developer tools faster is not impact ambition. Positional is a counterexample in one respect: it is an app for end users (hikers, pilots, photographers, anyone who wants a compass, a sun position or a level), not a tool for developers. But the *migration itself* is internal-value work. Its justification has to come from outcomes users or maintainers can feel: battery use, startup time, APK size, crash rate, ability to add features faster, ease of contribution. Those are all measurable, and none are measured in the repository yet beyond the intent stated in commit messages. In Dan's terms this is "works for me" quality, which is fine, but it means the experiment so far shows *feasibility and cost*, not *benefit*.

---

## Part 5: What the experiment does not show

It is worth being explicit about the limits, partly because Dan's post is itself explicit about its limits.

- **No before-and-after measurements.** There are no benchmarks in the repository for cold start, frame time, battery drain over a trail recording, APK size against upstream, or sensor latency. Phase 21 targets size and load time, which suggests that someone is measuring informally, but it is not recorded as results.
- **A single maintainer, a single app, a single model family.** Everything in the transcripts is Claude; there is no N-version cross-check, which is the strongest verification idea that came out of the thread. A second model reviewing each phase against the original behaviour would be a cheap experiment.
- **The history is not a clean experiment.** The repository's total commit count is far higher than three days of prompt-response cycles would produce, which suggests upstream history is included. Without separating the two, one cannot say precisely how many commits belong to the migration.
- **Upstream parity is unverified here.** Nothing in the repository proves that every upstream feature behaves identically after the migration; the tests encode what the model and maintainer thought mattered.
- **User-facing risk is untested in public.** Because it ships a full release on every push, any regression reaches anyone who installed the APK, directly or through Obtainium. That is a deliberate choice; it is also an exposure that grows with the user base.
- **Cost and sustainability are partly visible.** Transcripts show weekly usage climbing past 80 per cent on a consumer plan late in the sequence. That is cheap compared with months of labour, but it is a real budget, and it may be what ended up setting the pace.

---

## Part 6: Lessons for anyone attempting something similar

1. **Pick work with an oracle.** A migration of a working app is a good candidate because the old behaviour is the spec. Write down the invariants (database schema, preference keys, widget receiver names so placed widgets keep working) and make CI enforce them.
2. **Make the whole code base fit in context.** The `export.sh` and `dump.txt` approach is simple and effective. The standing "no code comments, no dead code, be concise" rules are not stylistic; they keep the dump inside the model's working budget.
3. **Phase it, and let each phase be small and nameable.** The table of phases is the project's best artefact. It enabled partial reverts, kept each change reviewable, and let the project use temporary bridges (hosting panels with `AndroidFragment` in phase 8g, removing fragments entirely in 8h).
4. **Turn requests into lint failures.** "Remove unused resources" is a request; "unused resources fail the release build" is a guarantee.
5. **Invest in the verifier, not just the generator.** The thread's best comments, and Dan's own appendix, agree that the scarce thing is cheap, trustworthy checking: property tests, differential tests, replicas, schema gates, real-device smoke tests.
6. **Watch for scope creep dressed as progress.** Write the original goal at the top of the README and ask of each new phase whether it serves it.
7. **Publish your process if you want to be believed.** Provenance is cheap to provide and expensive to fake.
8. **Do not stop at feasibility.** Pick three or four user-visible metrics (cold start, APK size, battery over a one-hour trail, crash-free sessions) and measure them against the last upstream release. That would turn "it can be done" into "it was worth doing".

---

## Part 7: Where this leaves the argument

Dan Luu's essay and Yossi Kreinin's appendix are often presented as opposites, but the experiment suggests they describe the same constraint from different sides. Cheap generation makes more projects feasible, which is Dan's point. It also makes the *unverified* part of every project larger, which is Yossi's. The projects that hold up, in the thread and in this repository, are the ones where someone built the checks at the same time as the code.

The Hacker News thread also sharpened a question the essay leaves open: what should we be ambitious *about*? A faster regex engine, a faster Rust build, a Compose migration and a Postgres rewrite are all things that become cheaper. Whether they matter depends on who ends up using them. The most encouraging version of the experiment is the one where the migrated app is measurably faster, lighter and easier to extend, and where the same method is then pointed at problems for people who will never read a changelog.

---

## Sources and links

- Dan Luu, "We should be more ambitious with the software we build" — [danluu.com](https://danluu.com/) (find the post on the site; the Hacker News submission was by `ltratt` on 25 October 2026)
- Hacker News discussion of that post — [news.ycombinator.com](https://news.ycombinator.com/) (search for the title; roughly 450 points and 410 comments at the time of reading)
- Related earlier thread on the pgrust ClickBench controversy — [news.ycombinator.com/item?id=50205816](https://news.ycombinator.com/item?id=50205816)
- The experiment — [github.com/kusl/hamza417-positional](https://github.com/kusl/hamza417-positional)
- The upstream project — [github.com/Hamza417/Positional](https://github.com/Hamza417/Positional)

*Facts about the repository were read from its public GitHub data on 25 October 2026: the README, commit history, language statistics, file tree and the Claude transcripts under `docs/llm/`. Figures such as star counts, byte counts and commit counts will change. Statements about the Hacker News thread are paraphrases of the comments visible at the time of reading.*
