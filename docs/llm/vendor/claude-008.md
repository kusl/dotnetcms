27
04

this article 
https://matklad.github.io/2026/10/29/pub-calls-pub.html
source code at 
https://github.com/matklad/matklad.github.io/blob/master/content/posts/2026-10-29-pub-calls-pub.dj
says 

# Avoid Public Functions Calling Other Public Functions

A small-scale rule of thumb. This article will land much better with a concrete
example, but I am drawing a blank here, so you gotta just blindly trust my say
so!

When implementing a software module, it's often a case that you are writing a
public function `foo`, and at least part of what `foo` does is already handled
by another public function `bar`. The DRY instinct encourages you to call
`bar()` from within `foo`. This usually creates problems down the line. Instead,
create a new _private_ function and refactor both `foo` and `bar` to call that
shared helper:

```zig
// BAD
pub fn foo() void {
    return bar(.{ .frobnicate = true });
}

pub fn bar(options: struct {
    frobnicate: bool = false,
}) void {
    // ...
}
```

```zig
// GOOD
pub fn foo() void {
    return helper(.frobnicate);
}

pub fn bar() void {
    return helper(.transmogrify);
}

fn helper(mode: enum { frobnicate, transmogrify }) void {
    // ...
}
```

Rationale: the goal of a public API is to provide an interface that works best
at the call site. If parts of public API are re-used to implement the rest of
the API, then there's a tension between making the interface convenient for the
outside user and making it convenient to use from within abstraction itself.
Namely, functions tend to grow optional parameters that should never be set by
the user. Crucially, the parameter growth and interface degradation can happen
_after_ the original code sharing.

By proactively making the shared implementation private, the tension is defused.
You still might need to tinker with `helper` signature until it does everything
it needs to do, but, because the helper is private, this is a cheap, local
change.

A notable exception to this rule of thumb are "shortcut" APIs, where a public
function is _defined_ as a commonly used chain of calls to other public
functions. There, the rule is the opposite --- _no_ private calls are allowed,
to make sure that it is a genuine shortcut, and not a midlayer mistake.

P.S. While researching if anyone wrote a better blog post already, I stumbled
upon a fun stack overflow question, asking, paraphrasing:

> I've heard that it is bad when a public function calls another public
> function. Is this really so?

The most upvoted answer explains that it is indeed bad, because this makes
mocking to test a single method in isolation more complicated. Which is, imo, a
natural, but horrible consequence of misguided approach to testing. You
shouldn't be mocking your methods, you should mock your IO. And, similarly, you
shouldn't test methods, but rather software functionality (unless you are
building a public library with a stable API, which is a fun and important, but
very narrow fraction of software development).

The accepted answer says that this recommendation makes no sense if it is not
backed by an industry wide or an organizational standard. Which somehow manages
to make even less sense --- you shouldn't follow the guideline rules because
they are written in sanctified ink! Rather, you should apply each particular
rule because of the underlying reasons that motivate the rule. Guidelines and
best practices articles can concentrate lots of good reasoning in a single
place, but they don't absolve you from thinking!

---- 

However, we use public all the time in asp dotnet 
particularly when we have 
1. a public repository method 
2. a public service method that calls the said repository method 
3. bla bla bla and so on 

decide whether you support or oppose this blog post 
then
write a comprehensive article in markdown with plain text title and description
defending your position 
remember to cite all your sources 
and flag any unsourced claim as such 
































































































Claude
/

this article 
https://matklad.github.io/2026/10/29/pub-calls-pub.html
source code at 
https://github.com/matklad/matklad.github.io/blob/master/content/posts/2026-10-29-pub-calls-pub.dj
says 

# Avoid Public Functions Calling Other Public Functions

A small-scale rule of thumb. This article will land much better with a concrete
example, but I am drawing a blank here, so you gotta just blindly trust my say
so!

When implementing a software module, it's often a case that you are writing a
public function foo, and at least part of what foo does is already handled
by another public function bar. The DRY instinct encourages you to call
bar() from within foo. This usually creates problems down the line. Instead,
create a new _private_ function and refactor both foo and bar to call that
shared helper:

// BAD
pub fn foo() void {
    return bar(.{ .frobnicate = true });
}

pub fn bar(options: struct {
    frobnicate: bool = false,
}) void {
    // ...
}

// GOOD
pub fn foo() void {
    return helper(.frobnicate);
}

pub fn bar() void {
    return helper(.transmogrify);
}

fn helper(mode: enum { frobnicate, transmogrify }) void {
    // ...
}

Rationale: the goal of a public API is to provide an interface that works best
at the call site. If parts of public API are re-used to implement the rest of
the API, then there's a tension between making the interface convenient for the
outside user and making it convenient to use from within abstraction itself.
Namely, functions tend to grow optional parameters that should never be set by
the user. Crucially, the parameter growth and interface degradation can happen
_after_ the original code sharing.

By proactively making the shared implementation private, the tension is defused.
You still might need to tinker with helper signature until it does everything
it needs to do, but, because the helper is private, this is a cheap, local
change.

A notable exception to this rule of thumb are "shortcut" APIs, where a public
function is _defined_ as a commonly used chain of calls to other public
functions. There, the rule is the opposite --- _no_ private calls are allowed,
to make sure that it is a genuine shortcut, and not a midlayer mistake.

P.S. While researching if anyone wrote a better blog post already, I stumbled
upon a fun stack overflow question, asking, paraphrasing:

    I've heard that it is bad when a public function calls another public
    function. Is this really so?

The most upvoted answer explains that it is indeed bad, because this makes
mocking to test a single method in isolation more complicated. Which is, imo, a
natural, but horrible consequence of misguided approach to testing. You
shouldn't be mocking your methods, you should mock your IO. And, similarly, you
shouldn't test methods, but rather software functionality (unless you are
building a public library with a stable API, which is a fun and important, but
very narrow fraction of software development).

The accepted answer says that this recommendation makes no sense if it is not
backed by an industry wide or an organizational standard. Which somehow manages
to make even less sense --- you shouldn't follow the guideline rules because
they are written in sanctified ink! Rather, you should apply each particular
rule because of the underlying reasons that motivate the rule. Guidelines and
best practices articles can concentrate lots of good reasoning in a single
place, but they don't absolve you from thinking!

---- 

However, we use public all the time in asp dotnet 
particularly when we have 
1. a public repository method 
2. a public service method that calls the said repository method 
3. bla bla bla and so on 

decide whether you support or oppose this blog post 
then
write a comprehensive article in markdown with plain text title and description
defending your position 
remember to cite all your sources 
and flag any unsourced claim as such
Searched the web, read 2 pages

Title:
Should Public Methods Call Public Methods? A .NET Reading of matklad’s Rule

Summary:
Alexey Kladov (matklad) advises against public functions calling other public functions. I support the rule, but only inside a single module. The usual ASP.NET pattern of service calls repository crosses a layer boundary, so the rule does not forbid it. The article uses this blog’s own source code to show where the rule bites, where it doesn’t, and where I think the post overreaches. Every claim is cited or flagged as unsourced.

Article body (Markdown, written for your blog’s parser: one line per paragraph, no nested lists, no multi-line blockquotes):
markdown

## My verdict

I support matklad's rule, with one clarification: it governs calls inside a module, not calls between layers. The ASP.NET pattern of controller or page calls service calls repository does not violate it. The rule does apply to a service whose public methods call each other.

Citations are inline and listed at the end. Anything I could not source is marked **Unsourced** and collected in a final section.

## What the post actually says

Matklad's post, [Avoid Public Functions Calling Other Public Functions](https://matklad.github.io/2026/10/29/pub-calls-pub.html), proposes a rule of thumb. When a public `foo` needs behavior that public `bar` already has, don't call `bar` from `foo`. Extract a private helper and have both call it.

His reasoning is that a public API should be shaped for its outside callers. If the API is also used to implement itself, internal needs push in optional parameters that outside users should never set. That degradation can happen after the original code sharing. A private helper keeps changes cheap and local.

He carves out one exception: "shortcut" APIs, defined as a common chain of other public calls. Those should use no private calls, so they remain genuine shortcuts. He also admits he could not come up with a concrete example.

## The objection: we do this constantly in ASP.NET

The typical stack looks like this:

1. A public repository method.
2. A public service method that calls it.
3. A page or controller that calls the service.

That is public calling public, three times over. Does the rule condemn the whole architecture? No, and the reason is the scope of the rule.

## Why layered calls are a different thing

Matklad's rule is about a module reusing its own public surface to implement itself. A service calling a repository is one module using another module's published contract, which is what that contract is for.

The layering is deliberate. Martin Fowler's catalog describes the [Repository pattern](https://www.martinfowler.com/eaaCatalog/repository.html) as mediating between the domain and data-mapping layers through a collection-like interface. Microsoft's [.NET microservices guidance](https://learn.microsoft.com/en-us/dotnet/standard/microservices-architecture/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design) says persistence abstractions are defined as interfaces in the domain model and implemented by adapters elsewhere. The caller is supposed to go through the public interface.

This blog follows that shape. Its [ADR-0002](https://github.com/kusl/dotnetcms) (`docs/adr/0002-clean-architecture-layering.md`) routes every cross-layer collaboration through an interface declared in `Core`, such as `IPostRepository` and `IAuthService`. Calling those is the architecture working as designed.

There is a real hazard in layered code, but it is a different one. John Ousterhout's *A Philosophy of Software Design* argues that adjacent layers should offer different abstractions, and it names the failure mode: pass-through methods that do little beyond forwarding to another method with a similar signature. Secondary summaries of the book are in [Henrik Warne's review](https://henrikwarne.com/2021/07/12/book-review-a-philosophy-of-software-design/) and [this chapter summary](https://dev.to/markadel/a-philosophy-of-software-design-summary-pk9). The test for a service method is whether it adds something, such as validation, orchestration, a transaction boundary or a policy. If it only forwards, the problem is a shallow layer, not a public-calls-public violation.

In this codebase, `AuthService.ChangePasswordAsync` loads the user, verifies the current password, hashes the new one and saves. That is a real abstraction. The Blazor pages, meanwhile, inject `IPostRepository` directly with no post service in between. That avoids pass-through methods, and it is a reasonable choice at this size.

## Where the rule does bite in ASP.NET

The rule applies when one class's public methods call each other. This sketch is my own illustration:

```csharp
// BAD: the public API grew a parameter that only internal callers need
public sealed class PostService(IPostRepository posts)
{
    public Task<PostPage> GetHomePageAsync(int page) =>
        GetPostsAsync(page, pageSize: 10, includeDrafts: false, forFeed: false);

    public Task<PostPage> GetPostsAsync(int page, int pageSize, bool includeDrafts, bool forFeed)
    {
        // ...
    }
}
```

```csharp
// GOOD: callers see small purpose-built methods; the knobs live in a private helper
public sealed class PostService(IPostRepository posts)
{
    public Task<PostPage> GetHomePageAsync(int page) =>
        LoadAsync(new PostQuery(page, PageSize: 10, IncludeDrafts: false));

    public Task<PostPage> GetAdminListAsync(int page) =>
        LoadAsync(new PostQuery(page, PageSize: 50, IncludeDrafts: true));

    private Task<PostPage> LoadAsync(PostQuery query)
    {
        // ...
    }
}
```

In the first version, `forFeed` and `includeDrafts` leak to every consumer, and someone eventually calls `GetPostsAsync(..., includeDrafts: true, ...)` from a public page. That is the failure matklad describes.

The Java world has a second, concrete reason. Mark Spritzler's [TheServerSide article](https://www.theserverside.com/opinion/Use-Java-method-modifiers-to-avoid-public-method-antipattern) shows a Spring service whose public method calls another public method in the same class. Because Spring adds transactions through proxies, the internal call bypasses the proxy and the second method's `REQUIRES_NEW` transaction is never started. His fixes are a private shared method or moving one method to another class.

**Unsourced:** the .NET analogue, where decorator-style DI registrations or interceptors wrap the interface but a `this.Method()` call inside the class skips them. This follows from how interface-based decoration works, but I did not find a .NET source stating it, so treat it as my reasoning.

## Evidence from this blog's source

I checked the repository dump at commit `f1cc458`. These are observations about the code, not external claims.

| Code | Shape | Reading |
|---|---|---|
| `LoginRateLimitMiddleware` | Two public constructors chain to one private constructor | Matches the rule exactly |
| `RssEndpoints` | Private `GenerateRssFeed` calls public `BuildFeedAsync` | Private calling public, the allowed direction; `BuildFeedAsync` is public so tests exercise production code (ADR-0022) |
| `TelemetryPathResolver` | Calls `DatabasePathResolver.GetPreferredDataDirectory`, which is `internal` | Shared logic exposed through a narrow seam instead of the public API |
| `DatabasePathResolver` | Public `GetDatabasePath` calls public `GetDataDirectory` | Public calling public, but arguably a "shortcut" API in matklad's sense, since the path is the directory plus a fixed file name |
| `PostRepository` | Three methods repeat the same projection to `PostListItemDto` | Not public-calls-public, but the DRY pressure matklad describes; a private projection helper is the rule-compatible fix |

None of these is a violation that hurts today. The `PostRepository` duplication is the one place where the rule would suggest an actual change.

## Where I would push back on the post

**1. The cost argument is weaker for application code than for libraries.** Fowler's [Public versus Published Interfaces](https://martinfowler.com/bliki/PublishedInterface.html) distinguishes public from published. A published interface is used outside the codebase that defines it, and he argues that distinction matters more than public versus private. For an unpublished interface where you can find and change every caller, changing it is cheap. In an app like this blog, with one assembly of callers, a public method that grew a bad parameter can be fixed by editing its callers. The rule is stronger for library APIs than for app-internal code.

**2. "Usually creates problems down the line" is an assertion, not a measurement.** The post offers no data or concrete example, and says so. I found no empirical study. Treat the rule as sound experience-based advice, not established fact.

**3. The testing aside is opinion.** Matklad argues that you should mock IO rather than methods and test functionality rather than individual methods. That is a defensible position, but the post presents it as obvious. I could not locate the Stack Overflow thread he paraphrases, so I am relying on his summary of it.

**4. The word "module" does the real work.** The rule is clear for a class or file. It becomes vague across assemblies, where "public" can mean public to the whole solution or published to the world. His own "midlayer mistake" reference, from Neil Brown's [Linux kernel design patterns, part 3](https://lwn.net/Articles/336262/), argues that shared behavior belongs in library routines each caller can use or ignore, not in a mandatory middle layer. That is a related idea, but it is about subsystem structure, not method visibility.

## A practical checklist

1. Calling another class through its interface is fine. That is layering, and it is what the repository pattern is for.
2. Calling a public method on `this` is the case to question.
3. If a public method has parameters only internal callers use, extract a private helper.
4. If a service method only forwards to a repository method, consider removing the service layer for that operation.
5. If a method really is a shortcut for a chain of public calls, keep it public-only, as matklad says.
6. For library or published APIs, apply the rule strictly. For app-internal code, apply it where it prevents parameter creep.

## Unsourced claims

- The .NET analogue of the Spring proxy self-invocation problem (my reasoning).
- That `forFeed` and `includeDrafts` style parameters tend to be misused by later callers (illustrative, from the matklad post's general argument, not measured).
- The matklad post's Stack Overflow quotations, which I could not verify directly.
- That the rule is "sound experience-based advice". This is my judgment; I found no empirical backing.
- That the `PostRepository` duplication is worth fixing. This is a style judgment.

## Sources

- [Avoid Public Functions Calling Other Public Functions](https://matklad.github.io/2026/10/29/pub-calls-pub.html), matklad, 2026-10-29.
- [Repository](https://www.martinfowler.com/eaaCatalog/repository.html), Martin Fowler, Patterns of Enterprise Application Architecture catalog.
- [Design the infrastructure persistence layer](https://learn.microsoft.com/en-us/dotnet/standard/microservices-architecture/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design), Microsoft Learn.
- [Public versus Published Interfaces](https://martinfowler.com/bliki/PublishedInterface.html), Martin Fowler.
- [Book Review: A Philosophy of Software Design](https://henrikwarne.com/2021/07/12/book-review-a-philosophy-of-software-design/), Henrik Warne (secondary source for Ousterhout).
- [A Philosophy of Software Design Summary](https://dev.to/markadel/a-philosophy-of-software-design-summary-pk9), Mark Adel (secondary source for Ousterhout).
- [Use Java method modifiers to avoid public method antipattern](https://www.theserverside.com/opinion/Use-Java-method-modifiers-to-avoid-public-method-antipattern), Mark Spritzler, TheServerSide, 2021.
- [Linux kernel design patterns, part 3](https://lwn.net/Articles/336262/), Neil Brown, LWN, 2009.
- This blog's source and ADRs: [github.com/kusl/dotnetcms](https://github.com/kusl/dotnetcms), commit `f1cc458`.

Two notes:

    Ousterhout is secondary-sourced. I cited summaries of his book, not the book itself, and the article says so. If you own it, swap in page references.
    Stack Overflow thread. I couldn’t find the question matklad paraphrases, so the article doesn’t quote it.

Claude works directly with your codebase


Claude is AI and can make mistakes.
Claude finished the response

















































Claude
/

this article 
https://matklad.github.io/2026/10/29/pub-calls-pub.html
source code at 
https://github.com/matklad/matklad.github.io/blob/master/content/posts/2026-10-29-pub-calls-pub.dj
says 

# Avoid Public Functions Calling Other Public Functions

A small-scale rule of thumb. This article will land much better with a concrete
example, but I am drawing a blank here, so you gotta just blindly trust my say
so!

When implementing a software module, it's often a case that you are writing a
public function foo, and at least part of what foo does is already handled
by another public function bar. The DRY instinct encourages you to call
bar() from within foo. This usually creates problems down the line. Instead,
create a new _private_ function and refactor both foo and bar to call that
shared helper:

// BAD
pub fn foo() void {
    return bar(.{ .frobnicate = true });
}

pub fn bar(options: struct {
    frobnicate: bool = false,
}) void {
    // ...
}

// GOOD
pub fn foo() void {
    return helper(.frobnicate);
}

pub fn bar() void {
    return helper(.transmogrify);
}

fn helper(mode: enum { frobnicate, transmogrify }) void {
    // ...
}

Rationale: the goal of a public API is to provide an interface that works best
at the call site. If parts of public API are re-used to implement the rest of
the API, then there's a tension between making the interface convenient for the
outside user and making it convenient to use from within abstraction itself.
Namely, functions tend to grow optional parameters that should never be set by
the user. Crucially, the parameter growth and interface degradation can happen
_after_ the original code sharing.

By proactively making the shared implementation private, the tension is defused.
You still might need to tinker with helper signature until it does everything
it needs to do, but, because the helper is private, this is a cheap, local
change.

A notable exception to this rule of thumb are "shortcut" APIs, where a public
function is _defined_ as a commonly used chain of calls to other public
functions. There, the rule is the opposite --- _no_ private calls are allowed,
to make sure that it is a genuine shortcut, and not a midlayer mistake.

P.S. While researching if anyone wrote a better blog post already, I stumbled
upon a fun stack overflow question, asking, paraphrasing:

    I've heard that it is bad when a public function calls another public
    function. Is this really so?

The most upvoted answer explains that it is indeed bad, because this makes
mocking to test a single method in isolation more complicated. Which is, imo, a
natural, but horrible consequence of misguided approach to testing. You
shouldn't be mocking your methods, you should mock your IO. And, similarly, you
shouldn't test methods, but rather software functionality (unless you are
building a public library with a stable API, which is a fun and important, but
very narrow fraction of software development).

The accepted answer says that this recommendation makes no sense if it is not
backed by an industry wide or an organizational standard. Which somehow manages
to make even less sense --- you shouldn't follow the guideline rules because
they are written in sanctified ink! Rather, you should apply each particular
rule because of the underlying reasons that motivate the rule. Guidelines and
best practices articles can concentrate lots of good reasoning in a single
place, but they don't absolve you from thinking!

---- 

However, we use public all the time in asp dotnet 
particularly when we have 
1. a public repository method 
2. a public service method that calls the said repository method 
3. bla bla bla and so on 

decide whether you support or oppose this blog post 
then
write a comprehensive article in markdown with plain text title and description
defending your position 
remember to cite all your sources 
and flag any unsourced claim as such
Searched the web, read memory, and 5 more tools

Title

Should Public Functions Call Public Functions? A Defense of matklad’s Rule, and Why Your ASP.NET Layers Are Fine

Summary

matklad advises against public functions calling other public functions. I support the rule, but only as he scoped it: within a single module, where one public function reuses a sibling. A service method calling a repository method is a layer calling the layer beneath it, which is a different thing and is not a violation. The rule does apply in a few ASP.NET situations: sibling public methods in one service, self-calls that skip decorators, and facade methods. The article covers all three, with sources and flagged gaps.

Article body

The full markdown is in pub-calls-pub-aspnet.md. I checked the file and it contains no citation markup, only plain markdown links.

Flags

    Unsourced: matklad’s own claim that the pattern “usually creates problems” has no example behind it. I also could not find the Stack Overflow question he paraphrases.
    Inferred: the point that a this-call can bypass a decorator comes from Spring’s documentation, not from .NET docs. The article marks it as my inference.
    Secondary only: Ousterhout comes from a book review and reposted notes, not the book itself. The “repositories are not mandatory” line came from a mirror of Microsoft’s book, not the Microsoft page.
    Mine: the C# examples and the checklist are my own constructions.

Pub calls pub aspnet
Document·MD


Claude is AI and can make mistakes. Please double-check cited sources.
Claude finished the response









































































> **Disclaimer:** This is an opinion and analysis piece. Claims are linked to sources where I found them. Anything I could not source is marked **[UNSOURCED]** inline and listed again in the sourcing note at the end. Code samples are my own illustrations, not taken from any project.

## The short version

I **support** matklad's rule of thumb, but only within the scope he gave it. It does **not** conflict with the everyday ASP.NET pattern of a service method calling a repository method. A layer calling the layer below it is a different thing from one public function reusing a sibling public function in the same module. The rule does apply to ASP.NET code in a few places, and those are worth knowing about.

## What the post actually says

In ["Avoid Public Functions Calling Other Public Functions"](https://matklad.github.io/2026/10/29/pub-calls-pub.html) (Oct 29, 2026), matklad describes this situation: you are *implementing a module*, you write a public function `foo`, and part of what `foo` does is already done by another public function `bar`. DRY nudges you to have `foo` call `bar`. He advises extracting a private helper that both call instead.

His rationale is that a public API should be shaped for its outside callers. If the API is also reused to build the rest of the API, it gets pulled two ways, and functions tend to accumulate optional parameters that outside users should never set. He notes that this degradation can happen after the original sharing. A private helper can be reshaped cheaply because nobody outside depends on it.

He also names an exception: "shortcut" APIs, whose whole definition is a common chain of other public calls. For those he says the rule inverts, and private calls should be avoided so that the shortcut stays a genuine shortcut and not a midlayer mistake.

Two things about the post are worth stating plainly. First, the author admits he has no concrete example and asks readers to trust him. His claim that this "usually creates problems down the line" is therefore experience-based and I could not independently verify it **[UNSOURCED]**. Second, the scope is *within a module*, which matters for everything below.

## Why service → repository is not a violation

**1. It crosses a layer boundary; it does not reuse a sibling.**
Martin Fowler's [Service Layer](https://www.martinfowler.com/eaaCatalog/serviceLayer.html) pattern defines a boundary of services that sets out the operations available to client layers and coordinates the application's response to each. Microsoft's guidance on the [Repository pattern](https://learn.microsoft.com/en-us/dotnet/standard/microservices-architecture/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design) says a repository encapsulates a set of stored objects and the operations on them, and that in a DDD-style microservice the repositories should be the only channel used to update the database. In both cases the lower layer's public API exists *to be called by the layer above it*. That is its audience and its purpose.

**2. matklad's tension doesn't arise.**
His problem is one public API serving two audiences (outside users and the module's own internals). A repository's public methods are shaped for the service layer, and that is who calls them. (This is my reading of his rationale applied to layers, not something he wrote.)

**3. The layers are meant to be different abstractions.**
Ousterhout's *A Philosophy of Software Design* argues that adjacent layers should offer different abstractions. Summaries of the book call methods that merely forward to another method with a similar signature "pass-through methods" and treat them as a red flag ([Warne's review](https://henrikwarne.com/2021/07/12/book-review-a-philosophy-of-software-design/); [Lebrero's notes, as reposted](https://lemmy.federate.cc/post/3070128/7135858)). I did not read the book itself; these are secondary summaries.

## Where the rule does bite in ASP.NET code

These are the places where I think it applies. The examples are illustrative.

### a) Sibling public methods inside one service

```csharp
// Smells: the public single-item API is built on the public list API
public async Task<Order?> GetOrderAsync(int id) =>
    (await GetOrdersAsync(new OrderFilter { Ids = [id], IncludeDeleted = false,
                                            SkipAuthorization = true })).SingleOrDefault();

public async Task<IReadOnlyList<Order>> GetOrdersAsync(OrderFilter filter) { /* ... */ }
```

`SkipAuthorization` is exactly the kind of parameter matklad warns about: it exists only for internal reuse, yet it is now on a public type. The fix he proposes works in C# too:

```csharp
public Task<Order?> GetOrderAsync(int id) => LoadAsync(new Query(id, authorize: true));
public Task<IReadOnlyList<Order>> GetOrdersAsync(OrderFilter f) => LoadManyAsync(f);

private Task<Order?> LoadAsync(Query q) { /* shared logic */ }
```

### b) Self-calls and interception

ASP.NET Core's own documentation notes that putting the repository behind an abstraction makes it easy to extend behavior with [Decorators or Proxies](https://learn.microsoft.com/aspnet/core/fundamentals/repository-pattern). Proxy-based interception has a known weakness in the Java world: Spring's reference docs state that [self-invocation bypasses the advice](https://docs.spring.io/spring-framework/reference/7.1/core/aop/proxying.html), and that the [best approach is to refactor so self-invocation doesn't happen](https://docs.spring.io/spring-framework/reference/6.2.17/core/aop/proxying.html). By the same logic, if a public method on your service calls another public method via `this`, a decorator wrapped around the service's interface (logging, caching, retries) would not see the inner call. That conclusion is **my inference by analogy; I did not find .NET-specific documentation for it [UNSOURCED for .NET]**. It follows from how decorators work, but check your own container and interception library.

### c) The "shortcut" exception is real in services too

A facade such as `PlaceOrderAsync` that is defined as `ReserveStockAsync` followed by `ChargeAsync` followed by `SendReceiptAsync` should call only the public methods, so it behaves exactly as a caller composing them would. This follows matklad's exception. The example is mine.

## The midlayer connection

matklad ties the shortcut exception to the "midlayer mistake". Neil Brown's [2009 LWN article](https://lwn.net/Articles/336262/) argues that midlayers, which impose common functionality between a core and its drivers, are bad, because the midlayer cannot foresee everything the lower-level code might need. He argues for a library of support code that is available to drivers but not forced on them. In web terms, a "base service" or a generic `IService<T>` that every feature must pass through is a midlayer candidate. A private helper (or a plain library function) is the opposite: opt-in, local, and cheap to change. Mapping Brown's kernel argument onto ASP.NET base classes is my analogy, not his.

## Where I would push back on matklad

- **On mocking.** He writes that you should mock IO, not methods. I agree with the spirit, but Microsoft's EF Core testing guidance [recommends a repository as a seam](https://learn.microsoft.com/ef/core/testing/testing-without-the-database) so that tests can stub or mock the repository, and says the EF Core in-memory provider is strongly discouraged for testing. A repository interface *is* an IO boundary, so these views are compatible. They are different from mocking a service's own sibling methods, which is what he is objecting to.
- **On "no example needed".** The rule is plausible and the parameter-creep mechanism is easy to picture, but a rule of thumb is easier to apply when you can see it fail. Case (a) above is my attempt at that.
- **On the Stack Overflow question.** I could not locate the specific question he paraphrases, so I cannot confirm what the answers said or how they were voted **[UNSOURCED]**. His point that guidelines should be applied for their reasons, not because they are written down, stands on its own.

## A caution against over-applying the pattern you already use

Following the layers faithfully does not make every layer worthwhile. Microsoft's own guidance says custom repositories are [not mandatory](https://dzfweb.gitbooks.io/microsoft-microservices-book/content/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design.html) (I saw this on a mirror of the book, not on the Microsoft page itself), and that in EF the Unit of Work is the [`DbContext`](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core). A service method that only forwards to a repository method adds a name and nothing else. Ousterhout's pass-through warning applies here, and the cure is to ask what each layer hides, not to ban calls between layers.

## Practical checklist

1. **Calling down a layer** (controller → service → repository): fine. That is what the lower API is for.
2. **Public method calling a sibling public method in the same class:** pause. Does the callee have, or will it get, parameters that only internal callers need? If so, extract a private helper.
3. **Defining a convenience/facade method:** call only public APIs, so it stays a true shortcut.
4. **Using decorators, interceptors, or attributes that wrap services:** avoid `this`-calls to public methods that rely on that wrapping, or verify how your tooling handles them.
5. **If a layer only forwards:** question the layer.

## Sourcing note

**Verified from primary sources I read directly:** matklad's post and its stated rationale and exception; Fowler's Service Layer definition; Microsoft Learn pages on the infrastructure persistence layer, repository pattern, and EF Core testing; Neil Brown's LWN article thesis (via the LWN page and excerpts of it); Spring's reference documentation on self-invocation.

**Secondary sources:** Ousterhout's ideas are taken from a book review (Warne) and reposted reading notes (Lebrero), not the book. The "repositories are not mandatory" statement came from a gitbook mirror of the Microsoft book.

**Unsourced / flagged:** (1) matklad's claim that the pattern "usually creates problems", which he supports with no example; (2) the .NET-specific consequence of self-calls bypassing decorators, which is my inference from Spring's documentation; (3) the content and vote counts of the Stack Overflow question he mentions, which I could not find; (4) the C# code samples and the checklist, which are my own constructions; (5) the mappings of Brown's midlayer argument onto ASP.NET base classes.

**Not verified:** I did not check the Zig example's semantics, and I did not survey how common either pattern is in real ASP.NET codebases.










