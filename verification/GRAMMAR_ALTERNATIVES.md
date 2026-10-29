# Offline grammar alternatives

Checked 2026-10-24. No app integration or installation was changed.

## Observed results

- Tiny GEC alone: 13/15 synthetic grammar references matched; 10/15 correct control sentences preserved.
- Tiny GEC with the current native `SmartCleanup.Accept` check: 6/15 grammar references matched; 15/15 control sentences preserved. Rejected proposals fall back to the original. This is a narrow screen, not an overall reliability estimate.
- Harper live browser demo: 15 grammar sentences, capitalized and punctuated for the demo. It produced 10 problem entries, including useful explanations for `did not went`, `a apple`, and `since five years`. It also suggested `goes` for `He go to school yesterday`, missing the past-time context. Problem count is not correction accuracy.
- Harper controls: 15 correct sentences produced 4 suggestions, including a spelling suggestion for the name Sudheer. These are suggestions, not applied corrections. Do not automatically apply all spelling or repetition suggestions.

Evidence: `output/tiny-grammar-evaluation/guarded-results.json`, `harper-suggestions.json`, `harper-preservation.txt`, and `harper-demo.png`.

## Larger candidate

Unbabel T5-small, quantized ONNX conversion:
https://huggingface.co/TonyRaju/gec-t5-small-coedit-onnx-int8

The repository calls it coedit, but its documented original is `Unbabel/gec-t5_small`, not Grammarly CoEdIT. Use its `gec: ` input prefix. Full repository is listed as 152 MB. The test downloads only the encoder, decoder without cache, tokenizer and config assets, roughly 100 MB. Runtime and Python dependencies are additional and are not included in that number.

Run `Check alternative grammar model.cmd` with normal networking. It evaluates the same 30 cases and repeats with downloads disabled. Results: `output/unbabel-grammar-evaluation/result.json`.

After the user downloaded it, Unbabel inference completed locally with downloads disabled. The expanded 120-case suite contains 60 grammar references and 60 correct-text controls. Greedy decoding matched 58/60 grammar references and preserved 54/60 controls. Custom five-beam decoding matched 59/60 references and preserved 54/60 controls. Selected assets total 97,166,925 bytes (92.67 MiB), excluding runtime. Applying the current conservative native guard matched only 13/60 grammar references but preserved all 60 controls. These are author-written screening references, not a representative or independently reviewed accuracy benchmark. The custom beam search is not an exact reproduction of Transformers generation. No method is established as safe to silently rewrite all user speech.

Expanded evidence: `output/grammar-comparison/SUMMARY.md` and `summary.json`, with all inputs and outputs in per-method folders. Run `Check all offline grammar.cmd` to repeat the comparison. Notewll itself was not changed by this evaluation.

## Other approaches

Harper offers native Rust and local WebAssembly integrations, with targeted spans and explanations rather than whole-passage generation:
https://writewithharper.com/

LanguageTool can run locally, but requires Java; its self-hosted distribution does not include cloud-only AI rules. It is less attractive for a very small installer:
https://dev.languagetool.org/http-server

Practical release direction: keep verbatim and basic cleanup lightweight; offer an optional, locally executed grammar pack after measured comparison. Protect factual fields, retain originals and expose uncertain changes for review. No option guarantees correction of every error.
