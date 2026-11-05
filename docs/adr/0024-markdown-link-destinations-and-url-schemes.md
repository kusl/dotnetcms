---
status: accepted
date: 2026-11-04
deciders: Kushal (with AI assistance from Claude)
tags: [content, markdown, security, xss, seo]
---

# 0024 — Markdown link destinations, URL scheme allow-list, escapes, and multi-line blockquotes

## Context and Problem Statement

A post linked to `https://en.wikipedia.org/wiki/Reconciliation_(United_States_Congress)`. The link and image patterns stopped the URL at the first `)`, so the href lost its closing parenthesis and a stray `)` was left in the text. Authors had to percent-encode parentheses as `%28`/`%29`.

The same review found related defects:

- Link and image titles (`[x](url "title")`) were folded into the href.
- `\*`, `\_`, `\[` and other backslash escapes rendered with the backslash.
- Image alt text containing inline code leaked an unrestored placeholder token.
- `<https://…>` autolinks rendered as literal text.
- Consecutive `>` lines became one blockquote per line; `>` without a space was not a quote.
- `## Title ##` kept the closing hashes; `* * *` and `- - -` became list items; `+` bullets were not lists.
- URLs were not scheme-filtered (ADR-0007).
- The post page (`og:image`) and the image cache warmer each parsed image URLs with their own regex, which had the same parenthesis bug, matched images inside code, and ignored titles. The post page also kept the previous post's preview image when navigating to a post without one.

## Decision Outcome

Follow CommonMark for these constructs, still without a third-party library (ADR-0006).

**Link and image destinations** (`LinkPattern`, `ImagePattern`) use .NET balancing groups:
- A bare destination may contain balanced, nested parentheses and no whitespace. Unbalanced parentheses mean the text is not a link.
- `<…>` destinations may contain spaces and any parentheses.
- An optional title in `"…"`, `'…'` or `(…)` becomes a `title` attribute.
- Link text may contain balanced brackets.

**Backslash escapes** for ASCII punctuation are protected in the same pass as inline code spans, so escapes do not apply inside code and an escaped backtick does not open a code span. `\)` in a destination is a literal parenthesis.

**URL scheme allow-list:** after escapes and HTML entities are resolved and control characters and spaces removed, a URL with a scheme must use `http`, `https` or `mailto`. Relative URLs, fragments and query strings are allowed. A rejected link renders its text only; a rejected image renders its alt text. `data:` images are rejected.

**Autolinks:** `<scheme:…>` and `<user@host>` render as anchors, subject to the same allow-list, and are protected from emphasis.

**Image URLs have one source of truth:** `IMarkdownService.GetImageUrls` returns the decoded, allowed image URLs outside fenced and inline code, in document order. `PostDetail` uses the first one for `og:image`/`twitter:image`, and `ImageCacheWarmerService` warms the same URLs that `ToHtmlAsync` looks up.

**Blocks:**
- Consecutive lines starting with `>` (optional single space) form one blockquote whose content is rendered recursively, so paragraphs, lists, headings, code fences and nested quotes work inside it. A blank line ends the quote.
- Closing hash sequences and trailing whitespace are stripped from ATX headings. `C#` is unaffected.
- Thematic breaks allow spaces between three or more of the same character.
- `+` is an unordered list marker.

### Consequences

- Good: Wikipedia-style URLs work as written; percent-encoded URLs still work.
- Good: closes the `javascript:` URL gap recorded in ADR-0007 and `solid-review.md`.
- Good: preview image and cache warmer agree with the renderer.
- Neutral: a backslash before ASCII punctuation is now consumed outside code. Write `\\` for a literal backslash, or use inline code for paths and regexes.
- Neutral: a bare destination containing a space (`[x](a b)`) is no longer a link; use `<a b>` or `%20`.
- Bad: still a subset. No nested lists, lazy blockquote continuation, reference links, setext headings or bare-URL autolinking.

## More Information

Amends ADR-0007 (URL scheme filtering, blockquotes) and ADR-0023 (protected spans). Tests: `MarkdownServiceLinkTests`, `MarkdownServiceBlockTests`.

**Y-statement:** In the context of admin-authored Markdown posts, facing URLs with parentheses that were truncated, unfiltered URL schemes and three diverging image-URL parsers, we decided for CommonMark-style balanced destinations, titles, escapes, autolinks and multi-line blockquotes with an http/https/mailto allow-list and a single `GetImageUrls` source, and neglected percent-encoding by authors and Markdig, to achieve links that work as pasted and safe output, accepting that backslashes before punctuation are now consumed, because the parser stays small, dependency-free and fully tested.
