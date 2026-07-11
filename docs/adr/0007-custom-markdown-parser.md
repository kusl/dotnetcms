---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [content, markdown, security, xss]
---

# 0007 — Hand-written Markdown parser

## Context and Problem Statement

Posts are authored in Markdown and rendered to HTML. ADR-0006 bans third-party
Markdown libraries (Markdig et al.). How is Markdown converted to HTML, and how is
the resulting HTML kept safe to inject into the page?

## Decision Drivers

- No third-party Markdown dependency (ADR-0006).
- Support the common subset actually used when blogging: headings, bold, italic,
  inline code, fenced code blocks, blockquotes, ordered/unordered lists, horizontal
  rules, links, and images.
- Output is injected via `(MarkupString)`, so it must not enable trivial HTML/JS
  injection from post content.
- Rendering must never throw and abort a page render.

## Considered Options

1. **Markdig** — full CommonMark, battle-tested sanitization. Rejected by ADR-0006.
2. **A hand-written line-oriented parser** using compiled `[GeneratedRegex]`.

## Decision Outcome

Chosen option: **a hand-written parser** (`MyBlog.Core/Services/MarkdownService.cs`,
`ToHtmlAsync`). It processes input line-by-line for block constructs and uses
compiled source-generated regexes for inline constructs. Its safety model is
**HTML-encode-first**: `ProcessInlineAsync` HTML-encodes `<`, `>`, `&`, and `"`
*before* any Markdown→HTML substitution, so raw HTML in a post is neutralized and
attribute values cannot break out. Unicode (e.g. emoji) is preserved by encoding
only those four characters rather than using a full HTML entity encoder. During
inline image handling the parser also calls the dimension probe (ADR-0008) to add
`width`/`height`, wrapped in try/catch so a probe failure still renders the image.

### Consequences

- Good: no external dependency; the parser is small and fully understood.
- Good: raw-HTML and quote-breakout XSS are prevented by encoding first; fenced
  code blocks and JSON-LD are also encoded/serialized safely.
- Bad — **known limitation (accepted):** link and image *URLs* are **not
  scheme-filtered**. A post containing `[x](javascript:alert(1))` renders an anchor
  with a `javascript:` href. This is tolerable **only** because of the trust model:
  all authors are authenticated admins (ADR-0011), so post content is trusted
  input, not attacker input. It would become a real vulnerability the moment
  untrusted users could author posts. **Future hardening:** allow-list
  `http`/`https`/`mailto`/relative URLs in `LinkPattern`/`ImagePattern` handling.
  This is deliberately deferred, not overlooked.
- Bad — **known limitation (accepted):** the parser supports a subset of Markdown.
  No tables, nested lists, task lists, reference links, or setext headings. Titles
  that render to an empty slug fall back to a UUID slug (`SlugService`).
- Neutral: `ToHtmlAsync` is asynchronous purely because inline image handling
  awaits the dimension probe; the text transformation itself is synchronous.

## More Information

Because `MarkdownService` depends on the scoped `IImageDimensionService`, it is
registered **scoped** (see `ServiceCollectionExtensions`), unlike the stateless
`SlugService` which is a singleton. The parser evolved from an initial synchronous
`ToHtml` using `HttpUtility.HtmlEncode` to the current async, emoji-preserving
version.

**Y-statement:** In the context of rendering admin-authored Markdown without a
third-party library, facing HTML-injection risk in `(MarkupString)` output, we
decided for a hand-written parser that HTML-encodes before substitution and
neglected Markdig, to achieve dependency-free rendering that is safe against
raw-HTML injection, accepting a Markdown subset and an unfiltered-URL-scheme gap
that is safe only under the admin-only trust model, because authors are trusted and
the gap is documented for future hardening rather than left implicit.
