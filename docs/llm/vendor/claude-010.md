14
45

Recently I ran into a problem with kphoto dot github dot io 
Claude fixed it in https://github.com/kphoto/kphoto.github.io/commit/ca040ef40bca9a0043b45f5c7d4e9b322c583300
my understanding of the problem was that we relied on corepack being there 
but it was removed with node 25 
please write a comprehensive article 
in plain English 
not just about the exact specifics of this problem 
but a more overall view as well 
some possible angles include 
- have a canary version of your project that ships early and ships often (to a non production environment) so we catch issues early
- have side projects or canary projects where you can ship early ship early and ship often
- build and maintain comprehensive automated test suites so when things break, they break early and loudly 
- reduce the number of dependencies 
- in a world where an LLM can do "all the work", perhaps the one work a human can do best is to care
there could be a lot of other angles here as well 
explore each and every angle thoroughly and make this a one stop shop complete explainer 
as usual please also have a plain text title and summary 
use all the markdown features we have added to our markdown service 
such as the nice table you added 








Claude
/
claude-010-kphoto.txt
claude-010-kphoto.txt
TXT
Recently I ran into a problem with kphoto dot github dot io 
Claude fixed it in https://github.com/kphoto/kphoto.github.io/commit/ca040ef40bca9a0043b45f5c7d4e9b322c583300
my understanding of the problem was that we relied on corepack being there 
but it was removed with node 25 
please write a comprehensive article 
in plain English 
not just about the exact specifics of this problem 
but a more overall view as well 
some possible angles include 
- have a canary version of your project that ships early and ships often (to a non production environment) so we catch issues early
- have side projects or canary projects where you can ship early ship early and ship often
- build and maintain comprehensive automated test suites so when things break, they break early and loudly 
- reduce the number of dependencies 
- in a world where an LLM can do "all the work", perhaps the one work a human can do best is to care
there could be a lot of other angles here as well 
explore each and every angle thoroughly and make this a one stop shop complete explainer 
as usual please also have a plain text title and summary 
use all the markdown features we have added to our markdown service 
such as the nice table you added

Ran 10 commands, used 2 tools, and 4 more tools
Now a quick fact-check on the Node.js corepack decision.
I’ve written the article. It’s built from the actual fix commit: the ADRs, changelog and lib.sh in your kphoto dump. The plain-text title and summary are at the top.

Title: The Day Corepack Disappeared: What a Missing Tool Teaches Us About Shipping Software

It runs in ten parts:

The incident. It has a timeline table and covers why your local podman run passed while CI failed. Podman kept its old copy of the v1.64.1-noble image after Microsoft republished that tag with Node 26. It also explains why the two green jobs were only lucky, and walks through the fix and the alternatives you rejected.
Invisible dependencies. Tools your build quietly assumes are installed, and how to write them down and pin them while still upgrading on purpose.
Canary builds. Scheduled CI runs, non-blocking jobs on the next runtime, and your planned .NET 11 release-candidate branch for dotnetcms as an example.
Side projects as early warning for enterprise work.
Tests that break early and loudly. This includes versionSync.test.ts as tests that check the build setup itself, plus useful error messages and proving local and CI really match.
Fewer dependencies. It uses the blog’s own hand-written Markdown parser as the example.
Reading the warnings that were already sent. Node announced the corepack removal in 2025.
Decision records.
The human job is to care. It’s frank that AI wrote the fix and lays out what still needs a person.
Honest trade-offs, followed by a 14-item checklist and references.
It only uses Markdown your parser supports: tables with alignment, blockquotes, fenced code, headings, lists, horizontal rules and links. Every paragraph is on a single line, because the parser treats consecutive lines as separate paragraphs. I left out nested lists and raw HTML, since the parser doesn’t support them.

One thing to check: the Node 26 LTS date (October 2026) comes from your ADR 0041; I didn’t look it up separately.

Files are in /mnt/user-data/outputs:

corepack-node-25-article.md
Sources:

kphoto commit ca040ef
Socket: Node.js TSC votes to stop distributing corepack
nodejs/TSC issue #1518

Corepack node 25 article
Document·MD 


Memory updated. View memory
Automatically approve is on. Claude runs on its own and pauses if anything looks unsafe. This includes using your connectors and Claude in Chrome, unless you turn them off. Learn more


Claude finished the response


corepack-node-25-article.md

dotnet-cms
1 document read
Web search

17
45































































































Now a quick fact-check on the Node.js corepack decision.I've written the article. It's built from the actual fix commit: the ADRs, changelog and `lib.sh` in your kphoto dump. The plain-text title and summary are at the top.

**Title:** The Day Corepack Disappeared: What a Missing Tool Teaches Us About Shipping Software

It runs in ten parts:

1. **The incident.** It has a timeline table and covers why your local podman run passed while CI failed. Podman kept its old copy of the `v1.64.1-noble` image after Microsoft republished that tag with Node 26. It also explains why the two green jobs were only lucky, and walks through the fix and the alternatives you rejected.
2. **Invisible dependencies.** Tools your build quietly assumes are installed, and how to write them down and pin them while still upgrading on purpose.
3. **Canary builds.** Scheduled CI runs, non-blocking jobs on the next runtime, and your planned .NET 11 release-candidate branch for dotnetcms as an example.
4. **Side projects as early warning** for enterprise work.
5. **Tests that break early and loudly.** This includes `versionSync.test.ts` as tests that check the build setup itself, plus useful error messages and proving local and CI really match.
6. **Fewer dependencies.** It uses the blog's own hand-written Markdown parser as the example.
7. **Reading the warnings that were already sent.** Node announced the corepack removal in 2025.
8. **Decision records.**
9. **The human job is to care.** It's frank that AI wrote the fix and lays out what still needs a person.
10. **Honest trade-offs**, followed by a 14-item checklist and references.

It only uses Markdown your parser supports: tables with alignment, blockquotes, fenced code, headings, lists, horizontal rules and links. Every paragraph is on a single line, because the parser treats consecutive lines as separate paragraphs. I left out nested lists and raw HTML, since the parser doesn't support them.

One thing to check: the Node 26 LTS date (October 2026) comes from your ADR 0041; I didn't look it up separately.

Files are in /mnt/user-data/outputs:
- corepack-node-25-article.md

Sources:
- [kphoto commit ca040ef](https://github.com/kphoto/kphoto.github.io/commit/ca040ef40bca9a0043b45f5c7d4e9b322c583300)
- [Socket: Node.js TSC votes to stop distributing corepack](https://socket.dev/blog/node-js-tsc-votes-to-stop-distributing-corepack)
- [nodejs/TSC issue #1518](https://github.com/nodejs/TSC/issues/1518)








































































TITLE:
The Day Corepack Disappeared: What a Missing Tool Teaches Us About Shipping Software

SUMMARY:
Node.js 25 stopped shipping corepack, and one morning a test job on kphoto.github.io failed with "corepack: not found". This is the full story of that failure, why it passed on my machine while failing in CI, and the broader lessons: canary builds, side projects as early warning systems, loud automated tests, fewer and pinned dependencies, and why caring is the job that stays human.

---

# The Day Corepack Disappeared

Most software failures are not dramatic. Nobody hacks you, no server catches fire. Instead, something you never thought about quietly stops being there, and a build that worked yesterday stops working today even though you did not change a single line.

That is what happened to my photo site, [kphoto.github.io](https://github.com/kphoto/kphoto.github.io), in early November 2026. The fix itself is in [commit ca040ef](https://github.com/kphoto/kphoto.github.io/commit/ca040ef40bca9a0043b45f5c7d4e9b322c583300). This article tells the story of that one small failure, and then steps back to look at what it says about building software in general. If you only want the short version, skip to the checklist at the end. If you want to understand why this keeps happening to everyone, read on.

## Part 1: What actually happened

### The setup

The site is a static photo gallery written in TypeScript. It uses Yarn 4 to install its dependencies. Yarn 4 is not something you usually install by hand. Instead, the project's `package.json` says which exact Yarn version it wants, and a small helper tool called **corepack** reads that line, downloads that exact Yarn, and runs it. This is a nice idea: everyone, on every machine, gets the same package manager without anyone installing anything globally.

The important detail is where corepack came from. For years, corepack was simply bundled with Node.js. If you had Node, you had corepack. You ran `corepack enable` once and forgot about it. The project's CI workflow did exactly that as its very first step.

### The change nobody on the project made

In 2025 the Node.js Technical Steering Committee [voted to stop distributing corepack](https://socket.dev/blog/node-js-tsc-votes-to-stop-distributing-corepack) with Node.js, starting with Node 25. Corepack itself still exists as a normal npm package; it just no longer comes in the box. Node 24 and earlier still bundle it. Node 25 and later do not.

That decision was public, discussed, and announced well in advance. But a decision in another project does not show up in your repository as a diff. It shows up later, as a red X.

### The timeline

| When | What happened | What we saw |
|:-----|:--------------|:------------|
| 2025 | Node.js decides corepack will not ship with Node 25 and later | Nothing. Our project used Node 24. |
| October 2026 | Node 26 becomes the active LTS release | Nothing yet. |
| 2026-11-01 | Microsoft republishes the Playwright test image `v1.64.1-noble` **under the same tag**, now with Node 26 instead of Node 24 | Nothing locally. |
| Next CI run | The end-to-end test job pulls the new image and runs `corepack enable` | `corepack: not found`, exit code 127 |
| Same time | The other CI jobs keep passing | Only because the GitHub runner's own Node still happened to include corepack |
| Locally | `./check.sh` passes on my Fedora machine | Podman reused its old copy of the same tag, still Node 24 |
| 2026-11-06 | Fix lands in commit ca040ef with ADRs 0039, 0040 and 0041 | Green again, and protected against a repeat |

### Why "it works on my machine" was technically true

This is the part that is worth slowing down for. My local check and CI were supposed to run in the **same container image**. That was a deliberate design decision, written down in an earlier decision record, precisely so that a green local run would mean a green CI run.

But the image was referenced by its tag, `v1.64.1-noble`, and tags are just labels. Anyone who owns the image can move a label to point at something new. When Microsoft rebuilt the image and moved the tag, GitHub Actions pulled the new one. Podman on my laptop saw that it already had an image with that tag and did not bother to pull again. Same name, two different images, two different versions of Node, two different results.

> A tag is a name. A digest is a fingerprint. Names can be reassigned; fingerprints cannot.

### Why the other jobs passing was the scariest part

Two of the three CI jobs kept passing. It would have been easy to look at that and think the problem was isolated to one job. In reality, those jobs were passing by luck. They ran on GitHub's runner image, whose preinstalled Node still bundled corepack. The day GitHub moved its runner image to a newer Node, those jobs would have failed in exactly the same way. The bug was not in one job. The bug was an assumption shared by all of them, and only one had been exposed so far.

### The fix

The fix did not try to put corepack back where it used to be. It removed the assumption that corepack is ever "just there."

| Problem | Fix | Where it is recorded |
|:--------|:----|:---------------------|
| Workflows assumed Node ships corepack | The project's own script installs an exact, pinned corepack version (0.36.0) into the user's cache folder, no root and no global install | ADR 0039 |
| The test image also shipped an old classic Yarn 1 that could be picked by accident | The pinned Yarn shim is put first on `PATH`, so any other `yarn` is shadowed | ADR 0039 |
| The image tag could change underneath us | The image is pinned by tag **and** digest, in both `compose.yaml` and the CI workflow | ADR 0040 |
| The Node version was hardcoded in three places | One `.node-version` file drives CI; the e2e job uses the image's own Node | ADR 0041 |
| Nothing stopped this from silently coming back | `versionSync.test.ts` fails if anything calls corepack directly, if a pin is not exact, or if an image has no digest | Changelog |

The heart of it is a small shell function. Every script calls it, so every environment gets Yarn the same way:

```bash
ensure_yarn() {
  local dir
  dir="$(tools_dir)"
  if [[ ! -x "$dir/bin/yarn" ]]; then
    command -v npm >/dev/null 2>&1 || die "npm not found; on Fedora: sudo dnf install nodejs-npm"
    log "installing corepack $KPHOTO_COREPACK_VERSION into $dir"
    rm -rf "$dir"
    mkdir -p "$dir/bin"
    npm install --prefix "$dir" --no-save --no-audit --no-fund --no-update-notifier --loglevel=error \
      "corepack@$KPHOTO_COREPACK_VERSION" >/dev/null \
      || die "could not install corepack $KPHOTO_COREPACK_VERSION from the npm registry"
    "$dir/node_modules/.bin/corepack" enable --install-directory "$dir/bin" yarn \
      || die "corepack could not create the yarn shim in $dir/bin"
  fi
  case ":$PATH:" in
    *":$dir/bin:"*) ;;
    *) export PATH="$dir/bin:$PATH" ;;
  esac
}
```

Notice what this does. It does not care which Node you have, which distro you are on, or whether you are in a container. It says, in plain terms: "I need this exact tool, at this exact version, and I will get it myself." The first run on a new machine downloads it once. Every run after that just adjusts `PATH`.

Alternatives were considered and rejected, and the reasons are worth knowing because you will face the same choices:

| Option | Why it was rejected |
|:-------|:--------------------|
| `npm install -g corepack` | Clashes with the yarn already in the image, needs root on Fedora, and behaves differently on every machine |
| Commit Yarn's release file into the repo | A 3 MB generated file in the repository and in every code dump |
| Pin the old Node 24 image | Moves backwards and only postpones the same failure to the next release |

That is the specific story. Now for the part that applies to every project, including the ones that have nothing to do with JavaScript.

---

## Part 2: The real lesson is about invisible dependencies

### Your project depends on far more than your dependency file says

When people talk about dependencies they usually mean the list in `package.json`, `Directory.Packages.props`, `requirements.txt` or `Cargo.toml`. Those are the **declared** dependencies. They are the easy ones, because you can see them, pin them and update them.

The dangerous dependencies are the **ambient** ones: things your project quietly assumes about the world. Corepack being bundled with Node was one of those. Nobody wrote it down. Nobody pinned it. It was just true, until it was not.

Here are some ambient dependencies that almost every project has without realizing it:

- The version of the language runtime that happens to be installed on the build machine.
- Tools that come preinstalled on a CI runner image, which the CI provider updates on its own schedule.
- A container image referenced by a tag that someone else can move.
- The default branch name, default shell, default locale, or default time zone of the build machine.
- A public package registry being reachable and fast.
- An API token that expires, or a free tier whose limits change.
- A certificate that renews automatically, until the renewal job breaks.

A useful exercise: pick one of your projects and ask, "If I built this on a brand new machine tomorrow, what would I have to install or configure that is not written down in the repository?" Every answer to that question is an ambient dependency, and every one of them is a future outage.

### Turning ambient dependencies into declared ones

The general fix is always the same shape as the corepack fix: make the assumption explicit, put it under version control, and give it one single source of truth.

| Ambient assumption | Declared version |
|:-------------------|:-----------------|
| "CI has Node 24" | A `.node-version` file read by CI and by local version managers |
| "Node comes with corepack" | A script that installs an exact corepack version |
| "This image tag means this image" | Tag plus `@sha256:` digest |
| "Everyone has the right .NET SDK" | A `global.json` with a pinned SDK version |
| "The runner has the right tools" | A container image, pinned by digest, that is the same locally and in CI |

None of this is exotic. It is just the discipline of refusing to let important facts live only in someone's head or on someone else's server.

### Pin, but keep moving

There is a natural overreaction to a story like this: pin everything forever and never upgrade. That trades a sudden failure for a slow one. A project pinned to old versions accumulates security holes and gets harder and harder to upgrade, until one day the upgrade is so big nobody wants to do it.

The healthier approach is to pin so that **change only happens when you choose it**, and then choose it often. The kphoto project already did this for its packages: every dependency is pinned to an exact version, and Yarn's 72-hour minimum-age gate means a newly published version is only picked up once it has been public for three days, which defeats many supply-chain attacks that rely on people installing a malicious version in the first few hours. Updating is a deliberate, reviewed act.

The corepack failure exposed the places where that discipline had not reached yet: the container image and the package manager bootstrap. After the fix, a republished image changes nothing until someone updates the digest, and when they do, it changes locally and in CI in the same commit.

> Pinning is not about never changing. It is about making sure every change is one you can see in a diff.

---

## Part 3: Ship early, ship often, somewhere it is safe to fail

### The idea of a canary

Coal miners used to carry canaries underground. The bird was more sensitive to dangerous gas than the miners were, so if the canary got sick, the miners got out. In software, a **canary** is any build, environment or deployment that is deliberately exposed to change before your important stuff is.

The point of a canary is not to avoid failures. The point is to **move failures earlier**, to a place where they are cheap. The corepack change was announced in 2025. It reached this project in November 2026. A canary build running against the newest Node release would have hit `corepack: not found` the week Node 25 came out, more than a year before it mattered, on a calm day, with no pressure.

### What a canary can look like

You do not need a big platform team to have canaries. Here are some options, from cheapest to most involved:

| Canary type | What it catches | Cost |
|:------------|:----------------|:-----|
| A scheduled CI run, even with no new commits | The world changing under you: new runner images, moved tags, expired tokens | Almost free |
| A CI job that runs on the newest runtime (current Node, next .NET preview) and is allowed to fail | Removed features and breaking changes, months early | Small |
| A branch that always tracks the latest version of every dependency | Upgrade pain, in small pieces instead of one big lump | Small to medium |
| A staging environment that deploys every commit | Configuration and deployment problems before production sees them | Medium |
| A real but low-stakes production site | Everything above, plus real hosting quirks | Medium |

The scheduled run is the one I would recommend to everyone first. A pipeline that only runs when you push code can only tell you about problems you caused. A pipeline that runs every night can tell you about problems other people caused, which in my experience is most of them. A minimal GitHub Actions version looks like this:

```yaml
on:
  push:
  pull_request:
  schedule:
    - cron: "17 6 * * *"
  workflow_dispatch:
```

One line of configuration, and your build now checks in on the world every morning whether or not you touched anything.

The second one, a job on the next runtime, is something I already plan for my blog engine, [dotnetcms](https://github.com/kusl/dotnetcms): a separate branch with its own CI workflow for the .NET 11 release candidate, while the main branch stays on .NET 10 until .NET 11 is stable. That is exactly the canary pattern. The release candidate branch is allowed to be red. Its job is to tell me what will break, long before it is my problem.

### A canary must be allowed to fail, but must not be ignored

There are two ways to get canaries wrong.

The first is making the canary job required, so that any breakage on a preview runtime blocks all your normal work. People will quickly start resenting the canary and eventually delete it.

The second is the opposite: making it optional and then never looking at it. A canary nobody watches is just a bird in a cage.

The middle path is to make canary failures **visible but not blocking**. Let them fail without stopping merges, but make sure a failure produces a notification someone actually reads, and treat a red canary as a ticket, not as noise.

---

## Part 4: Side projects are canaries for your career

### Why small projects find big problems first

Here is a pattern I have noticed. My personal and hobby projects tend to hit ecosystem changes before my work projects do. That is not a coincidence. Side projects usually:

- Use the newest versions of things, because there is no change board to convince.
- Have fewer people and less process, so upgrades happen in an evening instead of a quarter.
- Run on free tiers, public CI and public images, which change frequently and without notice.
- Are allowed to break, because nobody's paycheck depends on them.

That makes side projects excellent early warning systems. The kphoto site is a photo gallery. If it is broken for a day, nobody is harmed. But the lessons it taught me in that day, about mutable tags, about local caches lying to you, about ambient tools vanishing, apply directly to enterprise .NET work where an outage does matter.

When the same class of problem eventually reaches a big project at work, the person who has already seen it on a side project recognizes it in minutes instead of hours. You cannot buy that kind of pattern recognition. You can only earn it by shipping things, often, and watching them break.

### Make your side projects deliberately "close to the edge"

If you have side projects, consider tuning them to be good canaries:

- Run them on the latest LTS, or even the current release, of your runtime.
- Keep them up to date with automated dependency updates.
- Give them real CI with real tests, so breakage is noticed rather than discovered months later.
- Write down what broke and why, in a decision log, a changelog, or a blog post like this one.

That last point matters more than it seems. A failure you fixed and forgot teaches you once. A failure you wrote down teaches you, your future self, your teammates, and anyone who finds the write-up later.

---

## Part 5: Tests that break early and loudly

### Silence is the worst failure mode

There is a spectrum of how software fails:

| Failure style | Example | How bad |
|:--------------|:--------|:--------|
| Loud and early | CI fails with a clear message on the first commit that causes the problem | Best case |
| Loud but late | Production crashes and an alert fires | Painful but fixable |
| Quiet and early | A warning in a log nobody reads | Wasted opportunity |
| Quiet and late | Wrong data, slowly, for months | Worst case |

The corepack failure was a reasonably good failure. It was loud, it happened in CI rather than production, and the message, `corepack: not found`, pointed straight at the cause. That only happened because the project has an end-to-end test job that actually runs the full toolchain in a real environment. Without it, the first symptom might have been a broken deploy.

### Test the build, not just the code

Most people think of tests as checking that their code does the right thing. The kphoto fix added a different kind of test: one that checks that the **project's configuration** stays honest. The `versionSync.test.ts` suite now fails the normal unit test run if:

- A workflow, the compose file, or the shared script calls `corepack enable`, `prepare`, `install` or `use` directly.
- A workflow uses setup-node's built-in yarn cache, which needs a yarn before the scripts have provided one.
- The corepack or Yarn version is not pinned exactly.
- A container image reference has no digest, or the local and CI image references disagree.
- The Playwright image tag does not match the Playwright package version.
- A workflow hardcodes a Node version instead of reading `.node-version`.

This is a powerful idea that most teams never use. Any rule you rely on ("these two version numbers must match", "nobody should call this tool directly", "every image must be pinned") can be turned into a test. Then the rule is no longer something people need to remember. It is something the build enforces, every time, forever.

> If a rule matters enough to explain in a code review, it probably matters enough to be a test.

### Make error messages do the debugging for you

Look again at the `ensure_yarn` function above. Every place it can fail, it stops with a message that says what went wrong and, where possible, how to fix it: "npm not found; on Fedora: sudo dnf install nodejs-npm". Compare that to the original failure, a bare `not found` with exit code 127 in the middle of a CI log.

A good error message is a gift to the person who hits it, and very often that person is you, six months later, with no memory of how any of this works. Fail fast, fail loudly, and say something useful when you do.

### Make local and CI the same environment, really

The project already ran its end-to-end tests in the same container locally and in CI. The incident showed that "the same image name" is not the same as "the same image." After the fix, both places reference the image by digest, and a test fails if they ever disagree.

The general lesson: if you claim two environments are identical, find a way to **prove** it automatically. Otherwise you are just hoping.

---

## Part 6: Fewer dependencies, fewer surprises

### Every dependency is a promise someone else has to keep

Each dependency you add is a small bet that its maintainers will keep it working, keep it secure, keep it compatible, and keep it around. Most of the time that bet pays off. But the more bets you place, the more often one of them loses.

The corepack story is a good example of how this compounds. The project did not depend on corepack because anyone chose corepack. It depended on Yarn, and Yarn's recommended setup depended on corepack, and corepack's availability depended on Node's packaging decisions, and which Node you got depended on which container image Microsoft published under a tag. Four layers deep, and a change at the bottom broke the top.

### Questions to ask before adding a dependency

| Question | Why it matters |
|:---------|:---------------|
| Could I write the part I actually need in an afternoon? | Many packages are used for one small function |
| Who maintains it, and how many of them are there? | A single maintainer is a single point of failure |
| What does it pull in transitively? | One package can quietly bring dozens more |
| How often does it make breaking changes? | Fast-moving packages cost upgrade time forever |
| What happens to my project if it disappears tomorrow? | If the answer is "everything breaks", treat it as critical infrastructure |
| Is its license compatible with how I use it? | License surprises are worse than technical ones |

### A real example: writing your own Markdown parser

This very article is rendered by a Markdown parser I wrote by hand for my blog engine instead of using a popular library. That was a deliberate decision, recorded as an architecture decision record. The trade-off is honest: the parser supports a subset of Markdown, not all of it. Nested lists, reference links and raw HTML are not supported. Tables, which you have seen throughout this article, were added later because I needed them for research-style posts. Link handling was later hardened so that URLs with parentheses work as pasted and only safe URL schemes are allowed.

The benefit is that the parser is small, fully tested, and fully understood. When something renders wrong, there is no upstream project to wait on. There is one file and its tests.

This is not a rule that you should write everything yourself. Writing your own cryptography or database engine would be a terrible idea. The point is that dependencies have a cost, and for small, well-understood problems, the cost of owning the code can be lower than the cost of depending on someone else's.

### Dependency hygiene beyond "fewer"

When you do take a dependency, a few habits keep it from surprising you:

- Pin exact versions and commit the lockfile.
- Update on purpose, in small steps, with tests running.
- Use a minimum-age delay for new releases to dodge freshly published malicious versions.
- Pin transitive dependencies with known vulnerabilities rather than waiting for the top-level package to update. My blog engine does this through .NET Central Package Management with transitive pinning enabled.
- Keep a single source of truth for each version, so an upgrade touches one line instead of five.

---

## Part 7: Read the warnings that were already sent

The removal of corepack was not a secret. It was voted on in public, written about, and called out in Node.js release notes. The information existed more than a year before it hit this project. The problem was not a lack of information; it was that nobody was listening on the right channel.

Some cheap ways to hear about breaking changes before they hit you:

- Follow the release notes or announcement feeds of your runtime, your package manager, and your CI provider. Most of them publish RSS feeds or GitHub releases you can watch.
- Read deprecation warnings in your build logs and treat them as bugs. A deprecation warning is a breaking change with a countdown timer.
- Run your build on the next version early (see the canary section), because the build itself is the most reliable reader of release notes.
- When you upgrade a major version of anything, skim its list of removals, not just its list of new features.

> A deprecation warning is the cheapest bug report you will ever get. It arrives before the bug does.

---

## Part 8: Write down why, not just what

The fix for this incident was not only code. It was three new architecture decision records and amendments to four older ones. That might look like a lot of paperwork for a missing tool. It is not.

Six months from now, someone, quite possibly me, will look at `scripts/lib.sh` and wonder why on earth it installs its own copy of corepack instead of just calling `corepack enable` like the Yarn documentation says. Without a record, the "obvious cleanup" is to delete the workaround, and the bug comes right back. With a record, the answer is one file away: Node 25 removed corepack, here is the date, here is the failure, here are the alternatives we rejected and why.

Good decision records capture:

| Section | What it answers |
|:--------|:----------------|
| Context | What was the situation and what forced a decision? |
| Options considered | What else could we have done? |
| Decision | What did we actually choose? |
| Consequences | What gets better, what gets worse, what do we now have to remember? |

The "options considered" part is the most underrated. It saves the next person from re-researching the same dead ends.

And because decisions age, amending old records matters too. Several older records in this project, the ones that said "corepack needs enabling once per machine", were updated with a dated amendment rather than silently rewritten. The history of why the project changed its mind is preserved alongside the current truth.

---

## Part 9: In a world where an LLM can do the work, the human job is to care

### Who actually fixed this

I should be honest about something. I did not write most of this fix by hand. An AI assistant read the repository, diagnosed the failure, wrote the shell function, updated the workflows, added the guard tests, and drafted the decision records. That is increasingly how software gets built, and it is going to become more common, not less.

So it is fair to ask: if a model can do the work, what is left for the human?

### Caring is the work that does not delegate

Look at what actually had to happen for this fix to exist:

- Someone had to **notice** the red X, and not shrug it off because two of three jobs were still green.
- Someone had to be **bothered** by the fact that the local check passed while CI failed, instead of accepting "works on my machine."
- Someone had to **decide** that the right fix was to remove an assumption permanently rather than to pin the old image and move on.
- Someone had to **ask for more** than the minimum: guard tests, decision records, a digest pin, a single Node version file.
- Someone had to **check** the result: read the diff, run it locally, and confirm it actually worked.
- And someone had to care enough to write it all down afterwards, which is what you are reading.

None of those steps is typing code. All of them are caring. An AI model can produce a fix of almost any quality you ask for, but it produces what it is asked for. The quality of the request, the standard you hold the result to, and the willingness to keep pushing until the problem is solved properly rather than merely silenced: that comes from a person who cares about the outcome.

### What caring looks like in practice

| Low-care response | High-care response |
|:------------------|:-------------------|
| "Two jobs are green, it is probably a flaky image." | "Why are two jobs green? Are they safe or just lucky?" |
| "It works locally, CI must be weird." | "If local and CI disagree, our promise that they are the same is broken." |
| "Pin the old image and move on." | "What assumption failed, and how do we make sure it can never fail silently again?" |
| "The AI's fix built, ship it." | "Does this fix the cause? Did anything else change? Is there a test that would catch a regression?" |
| "Fixed. Done." | "Fixed, tested, documented, and written up so the next person does not repeat this." |

### The dangerous version of AI-assisted work

There is a failure mode worth naming. When code becomes cheap to produce, it becomes tempting to stop reading it. Ask for a fix, see green, merge. That works until the day the model confidently fixes the symptom instead of the cause, or "fixes" a test by weakening it, or makes ten unrelated changes alongside the one you needed.

The defense is the same set of habits this article has been describing: tests that enforce your rules, small reviewable changes, explicit pins, written decisions, and a human who actually looks. AI makes those habits more valuable, not less, because they are what lets you trust work you did not type yourself.

> The model can write the fix. Only you can decide that the fix is not good enough yet.

---

## Part 10: Trade-offs, honestly

None of these practices is free, and pretending otherwise makes people abandon them. Here is the honest accounting:

| Practice | Cost | Why it is usually worth it |
|:---------|:-----|:---------------------------|
| Scheduled CI runs | Some CI minutes, occasional noise | Catches the world changing, which is most breakage |
| Canary jobs on new runtimes | Extra configuration, red builds you must triage | Turns surprise outages into planned upgrades |
| Pinning by digest | Upgrades are now manual and you miss automatic security rebuilds | Changes only happen when you choose them, in one commit, everywhere |
| Guard tests for configuration | More tests to maintain | Rules enforce themselves instead of relying on memory |
| Fewer dependencies | More code you own | Fewer layers that can break under you |
| Decision records | Time to write | Saves far more time when someone asks "why?" later |
| Reviewing AI output carefully | Slower than blind merging | The only way to trust code you did not write |

A note on digest pinning specifically: the kphoto project accepted that a pinned test image will not pick up security rebuilds of the same tag automatically. That trade-off was acceptable because the image only runs tests, never the deployed site. For an image that serves production traffic, you would want automated tooling that proposes digest updates regularly, so you get both stability and freshness.

---

## The checklist

If you take nothing else from this article, take this list and run through it on one project this week.

1. List every tool your build uses that is **not** installed by the project itself. Each one is an ambient dependency.
2. Pin your runtime version in a file in the repository, and make CI read that file.
3. Reference container images by digest, not just by tag.
4. Make your local check and CI use the same environment, and add a test that proves it.
5. Add a scheduled CI run so your build checks in on the world even when you do not push.
6. Add an optional, non-blocking job on the next version of your runtime.
7. Turn any "remember to keep these in sync" rule into a test.
8. Make every script failure print what went wrong and how to fix it.
9. Before adding a dependency, ask what happens if it disappears tomorrow.
10. Subscribe to release notes for your runtime, package manager and CI provider.
11. Treat deprecation warnings as bugs with a deadline.
12. Write a short decision record whenever you work around something surprising.
13. Keep a side project close to the edge, and pay attention when it breaks.
14. When an AI writes the fix, read the fix.

---

## Closing thought

Corepack disappearing was a tiny event. One tool, one line in a workflow, one afternoon of fixing. But it carried almost every lesson in software reliability in miniature: invisible dependencies, mutable names, environments that drift apart while claiming to be identical, warnings that were sent but not heard, and tests that turned a potential silent failure into a loud and early one.

The tooling will keep changing. Node will remove other things. CI providers will update their images. Tags will move. You cannot stop any of that. What you can do is arrange things so that when the world changes, you find out early, in a safe place, with a clear message, and with enough written down that fixing it is a calm afternoon instead of an emergency.

And whether the fix is typed by you or by a model, the part that makes it a good fix is still the same: somebody who cares enough to make sure it never happens the same way twice.

---

## References

- [The fix: kphoto.github.io commit ca040ef](https://github.com/kphoto/kphoto.github.io/commit/ca040ef40bca9a0043b45f5c7d4e9b322c583300)
- [Node.js TSC votes to stop distributing corepack (Socket)](https://socket.dev/blog/node-js-tsc-votes-to-stop-distributing-corepack)
- [Node.js TSC discussion on corepack](https://github.com/nodejs/TSC/issues/1518)
- [kphoto.github.io repository](https://github.com/kphoto/kphoto.github.io)
- [dotnetcms, the blog engine that renders this post](https://github.com/kusl/dotnetcms)





























































17
45
