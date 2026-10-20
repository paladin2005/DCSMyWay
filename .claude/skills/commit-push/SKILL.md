---
name: commit-push
description: Create a git commit from the current working tree and push it to the remote, delegating to a Haiku subagent. Use whenever the user asks to commit and/or push the current changes, including the shorthands "commit", "commit & push" and "c&p".
allowed-tools: ["Agent"]
---

Delegate the entire commit + push workflow to a Haiku subagent so the main session stays on its current model.

## What to do

Make a single `Agent` tool call with:

- `subagent_type`: `"general-purpose"`
- `model`: `"haiku"`
- `description`: `"Create commit and push"`
- `prompt`: the self-contained instructions below (verbatim — the subagent has no conversation context)

Then relay the subagent's final report to the user. Do NOT run any git commands yourself.

## Subagent prompt (paste verbatim into `prompt`)

```
Create a git commit from the current working tree and push it to the remote. Use the Bash tool only.

Steps:
1. Run `git status` to see all modified/untracked files
2. If there are no changes at all (staged or unstaged), report this and stop
3. Stage all changes with `git add -A`
4. Run `git diff --cached` to analyze what will be committed
5. Create the commit using `git commit -m` with a HEREDOC for the message
6. Push to remote with `git push` (use `-u origin <branch>` if no upstream is set)

Commit message format: `type(scope): brief description`

Valid types: feat, fix, refactor, style, docs, chore, test, ci

Scope: primary area affected (window, aircraft, dcs, settings, build, etc.)

Rules:
- Brief description in English, lowercase, no trailing period
- Do NOT include Co-Authored-By or generated credits
- If multiple scopes are involved, use the most representative one
- Do NOT run any commands other than git

Report back the commit hash, the commit message, and confirmation that the push succeeded — or that there were no changes.
```
