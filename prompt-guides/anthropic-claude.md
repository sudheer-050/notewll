name: Claude
provider: Anthropic
wake: claude, hey claude, clawed, clod
apps: claude
titles: - Claude, Claude.ai, | Claude
ask: lead with the task, give the context and why it matters, state constraints and the output format, and put any material I included in XML tags
wrap: xml
version: 2026-11-20
---
Write the prompt the way Anthropic recommends prompting Claude:

- Be clear and direct. State the task in the first sentence, as you would brief a capable new colleague who has no context.
- Give the context and the reason behind the request (who it is for, what it will be used for, why it matters). Claude follows instructions better when it knows the goal.
- Be explicit about what a good result looks like: scope, level of detail, tone, and length. If the person wants Claude to go beyond the obvious, say so.
- Say what to do rather than what not to do.
- When the request includes material (text, code, data, an email), put that material in clearly labelled XML tags such as <document>, <code> or <email>, and keep the instructions outside the tags.
- For long material, put the material first and the question or instruction after it.
- Name the output format when the person implies one (bullet list, table, JSON, a short paragraph, a draft email).
- For complex reasoning or multi-step work, ask Claude to think through the problem step by step before answering.
- Include an example only when the person gave one or clearly wants a specific pattern.
- Use plain sentences. Short XML-tagged sections are fine for longer prompts; do not add headings to short ones.
