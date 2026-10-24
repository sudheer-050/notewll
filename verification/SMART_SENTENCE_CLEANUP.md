# Smart sentence cleanup verification - October 24, 2026

## What changed

- Removed the growing phrase-specific restart regexes. Basic cleanup now detects repeated grammatical prefixes and repeated anchors around abandoned fragments.
- Added a separate, local Ollama sentence-editing pass, independent of enhanced speech recognition. It reuses the installed `llama3.2` model on the standard loopback service, with matching warm-up/inference context settings.
- Smart cleanup runs in the background during shortcut dictation, then resolves only the latest complete text on release. No stale partial rewrite is applied. The extra final wait is bounded to four seconds; unavailable or slow processing keeps basic cleanup.
- Hands-free phrases and recovered dictation use the same guarded local sentence-editing pass. Original recognized text continues to be retained separately by the existing history/review flow.
- A token-alignment validator permits supported restart deletion, explicit pronoun agreement, punctuation/case correction and limited missing a/an insertion. It rejects changed content words, names, numerical facts, negatives, tense, reordered facts and unsupported preposition changes. This is a conservative constraint, not a proof of perfect semantic equivalence.
- If a model truncates a multi-sentence passage, each sentence is retried separately within the same model-processing budget, validated independently, then validated again as a complete passage.
- Preferences > Writing contains a Smart sentence cleanup toggle and a local-model check. Cleanup-off and Code mode preserve literal wording. The model is optional and is not bundled into the small update or installer. This PC already had it installed; no model download was needed for these checks.

## Measured evidence

- Full current application suite: **153 passed, 2 skipped, 2 deselected**. The two deselected tests require physical microphone access, which failed earlier in this managed environment.
- Real local-model results: `output/smart-cleanup/model-batch-final.json`.
- Eight warmed text-only requests measured **144-511 ms** in the final batch. This excludes ASR, microphone capture and desktop insertion. An earlier cold diagnostic took about 15 seconds; background warm-up is separate from the final release budget.
- Both reported examples produced the intended text:
  - `Can you list what are what updates you have done?` -> `Can you list what updates you have done?`
  - `Hey, how are you? I just wanted to talk to you about what are what what about the king?` -> `Hey, how are you? I just wanted to talk to you about the king.`
- Additional live-model checks corrected `I has` -> `I have`, `She go` -> `She goes`, and repeated multiword phrases, while preserving `not`, `42 dollars`, `Alex` and `before Friday` in the protected-facts case.
- Meaning-protection checks include altered names/numbers, removed negatives, changed prepositions, dropped invoice content, reordered names, altered tense, and intentional `had had` / `that that`.
- Rendered Writing preferences: `output/smart-cleanup/ui/System-Settings-Writing.png`.
- Installer: `dist/Notewll-Setup.exe`; small update: `dist/Notewll-Small-Update.zip`.

## Limits

This is broader sentence cleanup, not a guarantee of perfect interpretation. Ambiguous wording, unsupported edits, unavailable models and exhausted processing budgets can remain unchanged. Physical microphone-to-editor behavior still requires desktop acceptance. The installed app has not been replaced by this work.

### Repeated contracted question prefixes

Basic cleanup now recognizes contracted question words and treats straight/curly
apostrophes as equivalent when finding repeated prefixes. For example,
`Can you write me what's the what's the best restaurant near me?` becomes
`Can you write me what's the best restaurant near me?`. This runs without a model
and preserves the surrounding text, including negatives. Checks also cover mixed
apostrophes, comma-separated repeats, triple repeats and separate complete questions.
38 cleanup/live-grammar checks and 7 installer checks passed. Installer and small
update rebuilt; installed-app replacement remains separate.
