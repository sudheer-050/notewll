import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "build" / "Notewll.exe"
LONG = "write a summary of the quarterly sales report for my manager with three key risks"


def plan(mode, process, title, text):
    out = subprocess.run([str(APP), "--prompt-plan", mode, process, title, text], capture_output=True, text=True, timeout=60)
    assert out.returncode == 0, out.stderr
    return out.stdout.strip()


def test_every_guide_file_has_wake_words_and_guidance():
    guides = sorted((ROOT / "prompt-guides").glob("*.md"))
    assert len(guides) >= 7
    for guide in guides:
        head, _, body = guide.read_text(encoding="utf-8").partition("\n---")
        assert "name:" in head and "wake:" in head and len(body.strip()) > 100, guide.name
        assert guide.name.lower() != "claude.md", "claude.md would be read as a CLAUDE.md instructions file"


def test_raw_mode_never_rewrites():
    assert plan("Raw", "chrome", "Chat - Claude - Google Chrome", "Claude, " + LONG).startswith("plan=none")


@pytest.mark.parametrize("spoken,guide", [
    ("Claude, " + LONG, "Claude"),
    ("Hey ChatGPT " + LONG, "ChatGPT"),
    ("Gemini " + LONG, "Gemini"),
    ("Perplexity " + LONG, "Perplexity"),
])
def test_wake_word_selects_the_guide_and_is_removed(spoken, guide):
    result = plan("WakeWord", "notepad", "Untitled - Notepad", spoken)
    assert result.startswith("plan=" + guide + "|rewrite=True")
    assert result.endswith("text=" + LONG[0].upper() + LONG[1:])


@pytest.mark.parametrize("spoken", ["Cloud costs went up this quarter and we need to review them", "Claudette said she will join the meeting at noon tomorrow"])
def test_similar_words_do_not_trigger(spoken):
    assert plan("WakeWord", "notepad", "x", spoken).startswith("plan=none")


@pytest.mark.parametrize("spoken", ["Claude, hi", "Claude thank you", "ChatGPT, what time is it"])
def test_greetings_and_short_messages_are_typed_as_spoken(spoken):
    assert "rewrite=False" in plan("WakeWord", "notepad", "x", spoken)


def test_automatic_uses_the_window_but_not_other_sites():
    assert plan("Automatic", "chrome", "Plan - Claude - Google Chrome", LONG).startswith("plan=Claude|rewrite=True")
    assert plan("Automatic", "claude", "Claude", LONG).startswith("plan=Claude|rewrite=True")
    assert plan("Automatic", "chrome", "Inbox - Gmail - Google Chrome", LONG).startswith("plan=none")
    assert "rewrite=False" in plan("Automatic", "chrome", "ChatGPT - Google Chrome", "hi")
