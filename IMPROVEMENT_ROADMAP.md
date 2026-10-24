# Notewll improvement roadmap

Saved: October 24, 2026

These are proposed improvements, not a list of completed features.

## Direction

Make conversations easier to find, review and act on while keeping Notewll local and lightweight. Build on the existing dictation, notes, recordings, correction learning, exports and floating ball.

## Proposed improvements

| Improvement | What the user gets |
|---|---|
| **Search by meaning** | Ask “What was the deadline?” and find relevant statements even with different wording, with timestamps linking back to the conversation. |
| **Conversation highlights** | Short, timestamped excerpts covering important points, using extractive summarization without a large language model. |
| **Reviewable action items** | Suggested tasks, owners and dates alongside the original statement. Users confirm or correct them before they become tasks. |
| **Live bookmarks** | Press a shortcut during recording to mark “important,” “question” or “follow up,” then jump back to that moment. |
| **Projects and tags** | Group meetings, personal notes and dictation history by project, person or topic. Extend existing search with filters. |
| **Better correction learning** | Build on existing confirmed corrections: show what Notewll has learned, where a rule applies, and let users undo it. Preserve the original transcript. |
| **Hands-free app controls** | Choose which apps may receive hands-free text, pause on screen lock or microphone disconnection, and keep microphone status obvious. |
| **Private sharing** | Preview exactly what an export includes, exclude personal annotations, and redact selected text before sharing. Clearly distinguish text redaction from audio redaction. |
| **Model and storage manager** | Show download size, installed disk space and measured RAM separately. Let users install or remove optional features without affecting their notes. |
| **Stronger backup and recovery** | Extend existing backups with scheduled local snapshots, encryption and a restore preview. |

## Suggested implementation order

1. Finish real desktop testing of hands-free mode and add app controls.
2. Add live bookmarks.
3. Add semantic search across conversations.
4. Add lightweight highlights and reviewable action items.
5. Improve organization, sharing and backups.

## Strongest next feature

**“Find what I discussed and jump to the exact moment.”**

Connect recordings, transcripts, search and notes into one workflow without requiring a large chatbot model.

## Local implementation boundaries

- Keep conversation processing on the user's computer.
- Treat semantic retrieval as a way to find supporting passages; do not present it as unrestricted question answering.
- Present lightweight summaries as selected excerpts, rather than claiming they provide polished narrative synthesis.
- Link findings, highlights and suggested tasks to supporting statements and timestamps.
- Keep corrections and inferred action items reviewable. Preserve raw transcripts and source recordings.
- Measure the complete feature's download, installed size and RAM use. A model file's size alone does not represent total memory usage.

## Technical references

- [Sentence Transformers: semantic search](https://www.sbert.net/examples/sentence_transformer/applications/semantic-search/README.html)
- [TextRank: Bringing Order into Text](https://aclanthology.org/W04-3252/)

## Implementation update - October 24, 2026

The roadmap now has a local implementation covering the ten areas above. See
[implementation and verification status](verification/IMPROVEMENT_ROADMAP.md) for
what is implemented, exact artifacts, measured checks and remaining acceptance work.
Semantic model inference/resource measurement and real hands-free/recording desktop
tests remain pending; this is not a claim that every acceptance check is complete.
