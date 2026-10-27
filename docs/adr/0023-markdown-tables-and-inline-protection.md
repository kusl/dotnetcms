---
status: accepted
date: 2026-10-26
deciders: Kushal (with AI assistance from Claude)
tags: [content, markdown, css, responsive]
---

# 0023 — Markdown tables, protected inline spans, and ordered-list start numbers

## Context and Problem Statement

ADR-0007 accepted a Markdown subset with no tables. Research-style posts need tables of different sizes, often with links, emphasis and quotes in cells, and they must stay readable on phones. Rendering a real post also exposed three inline and list bugs:

- Emphasis regexes ran over generated HTML, so underscores in link URLs (`…/mp_/…/applicant_s2s/…`) were rewritten to `<em>` inside `href`, breaking the link. The same happened inside inline code and image alt text, and Markdown inside inline code was turned into links and images.
- `snake_case_words` were italicized mid-word.
- An ordered list resumed after a paragraph (`5. …`) restarted at 1.

How do we add tables and fix these without a third-party Markdown library (ADR-0006)?

## Decision Outcome

**Tables (GitHub Flavored Markdown syntax)** in `MarkdownService`:
- A table starts at a line containing `|` followed by a delimiter row containing `|` whose cells match `:?-+:?`. Header and delimiter cell counts must match; otherwise both lines render as ordinary paragraphs.
- Leading and trailing pipes are optional. `\|` puts a literal pipe in a cell.
- Body rows run until a blank line or a line that starts another block (fence, heading, blockquote, horizontal rule, list item). Short rows are padded with empty cells; extra cells are dropped.
- `:--`, `:-:` and `--:` emit `style="text-align: …"` on `th`/`td`. An inline style is used rather than a class so alignment survives in the RSS feed, where site CSS does not apply.
- Each cell goes through the same HTML-encode-first inline pipeline as paragraphs, including the image dimension probe.
- Output is `<div class="table-wrapper"><table><thead>…</thead><tbody>…</tbody></table></div>`; `tbody` is omitted for header-only tables.

**Responsive layout** in `site.css`: the wrapper scrolls horizontally (`overflow-x: auto`) inside the content column, so the page itself never scrolls sideways. The table fills the column; cells have a `6rem` minimum width, top alignment, `word-break: normal` (overriding the post page's `break-word`, which would split words letter by letter in narrow columns) and `overflow-wrap: break-word`. Colors come from the theme variables, so all six themes work.

**Protected inline spans:** `ProcessInlineAsync` replaces finished inline code, images and link opening tags with private-use placeholder tokens before bold and italic run, then restores them. Private-use characters in the input are emitted as numeric entities so they cannot collide with tokens. Link text still receives emphasis.

**Underscore emphasis** (`_x_`, `__x__`) is not applied when the delimiter touches a letter, digit or underscore. Asterisk emphasis is unchanged.

**Ordered lists** emit `start="n"` when the first item number is not 1.

### Consequences

- Good: tables of any size render, scroll on narrow screens, and keep working in RSS readers.
- Good: URLs, code and alt text are no longer altered by emphasis.
- Neutral: the header and delimiter rows must each contain a pipe. A line without pipes directly after a table becomes a row, as in GFM; leave a blank line before following text.
- Neutral: pipes inside inline code split cells unless escaped, as in GFM.
- Bad: still a subset. No nested lists, multi-line blockquotes, reference links or URL scheme filtering (ADR-0007 recommendation stands).

## More Information

Amends ADR-0007's "no tables" limitation. Tests: `MarkdownServiceTableTests`, `MarkdownServiceInlineTests`.

**Y-statement:** In the context of research posts that need tables, facing a dependency-free parser that had no tables and corrupted URLs with emphasis, we decided for GFM pipe tables in a horizontally scrolling wrapper, placeholder-protected inline spans, word-bounded underscore emphasis and ordered-list start numbers, and neglected Markdig and stacked "card" tables, to achieve readable tables on every screen and in RSS, accepting a few GFM edge-case differences, because the parser stays small and fully tested.
