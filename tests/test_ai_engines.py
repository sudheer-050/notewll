"""Own-key AI engines against a local fake of each provider API (no network, no real keys)."""
import json
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "build" / "Notewll.exe"
GUIDE = ("name: DeepSeek\nprovider: DeepSeek\nwake: deepseek, deep seek, hey deepseek\napps: deepseek\ntitles: DeepSeek\n"
         "ask: State the task plainly, then context, constraints and output format.\nwrap: quotes\nversion: 2026-11-20\n---\n"
         "Write the prompt the way DeepSeek recommends prompting DeepSeek:\n\n- Put the task first.\n- Give the context and constraints.\n"
         "- For reasoning models, state the goal directly and skip few-shot examples.\n- Ask for the output format explicitly.\n")
PROMPT = "I have a Python script that reads a CSV file and crashes on empty dates. Fix it. Return the updated code."
seen = []


class Fake(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def reply(self, obj, status=200):
        data = json.dumps(obj).encode()
        self.send_response(status); self.send_header("content-type", "application/json"); self.send_header("content-length", str(len(data))); self.end_headers(); self.wfile.write(data)

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers["content-length"])))
        h = {k.lower(): v for k, v in self.headers.items()}
        seen.append((self.path, h, body))
        if "bad-key" in json.dumps(h):
            return self.reply({"error": {"message": "invalid api key"}}, 401)
        p = self.path
        if p.startswith("/generativelanguage.googleapis.com/"):
            if body.get("tools"):
                return self.reply({"candidates": [{"content": {"parts": [{"text": GUIDE}]}, "groundingMetadata": {"groundingChunks": [{"web": {"title": "api-docs.deepseek.com"}}]}}]})
            return self.reply({"candidates": [{"content": {"parts": [{"text": PROMPT}]}}]})
        if p == "/api.anthropic.com/v1/messages":
            if body.get("tools"):
                if len(body["messages"]) == 1:  # first call pauses mid-search; the client must resend
                    return self.reply({"stop_reason": "pause_turn", "content": [{"type": "server_tool_use", "id": "s1", "name": "web_search", "input": {"query": "deepseek prompting"}}]})
                return self.reply({"stop_reason": "end_turn", "content": [
                    {"type": "web_search_tool_result", "tool_use_id": "s1", "content": [{"type": "web_search_result", "title": "DeepSeek API Docs", "url": "https://x"}]},
                    {"type": "text", "text": "Here is what I found."}, {"type": "server_tool_use", "id": "s2", "name": "web_search", "input": {}},
                    {"type": "text", "text": GUIDE}]})
            return self.reply({"stop_reason": "end_turn", "content": [{"type": "text", "text": PROMPT}]})
        if p == "/api.openai.com/v1/responses":
            return self.reply({"output": [{"type": "web_search_call"}, {"type": "message", "content": [{"type": "output_text", "text": GUIDE, "annotations": [{"type": "url_citation", "title": "DeepSeek docs"}]}]}]})
        if p.endswith("/chat/completions"):
            return self.reply({"choices": [{"message": {"role": "assistant", "content": PROMPT}}]})
        self.reply({"error": "unknown"}, 404)


@pytest.fixture(scope="module")
def fake():
    server = HTTPServer(("127.0.0.1", 0), Fake)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{server.server_port}"
    server.shutdown()


def run(fake, tmp_path, engine, key, model, url, action, text):
    seen.clear()
    out = subprocess.run([str(APP), "--engine-test", str(tmp_path), engine, key, model, url, action, text],
                         capture_output=True, text=True, timeout=60, env={**__import__("os").environ, "NOTEWLL_TEST_API_BASE": fake})
    return out.returncode, out.stdout + out.stderr


@pytest.mark.parametrize("engine,model,url", [("Gemini", "", ""), ("Anthropic", "", ""), ("OpenAI", "", ""), ("Compatible", "deepseek-chat", "https://api.deepseek.com/v1")])
def test_rewrite_with_each_provider(fake, tmp_path, engine, model, url):
    code, out = run(fake, tmp_path, engine, "sk-test-123", model, url, "rewrite", "my python script reads a csv and crashes on empty dates")
    assert code == 0, out
    assert "prompt:" + PROMPT in out
    assert "key-file-has-plain-key:False" in out  # stored encrypted
    path, headers, body = seen[-1]
    text = json.dumps(body)
    assert "Prompting guidelines for Claude" in text and "crashes on empty dates" in text
    if engine == "Anthropic":
        assert path == "/api.anthropic.com/v1/messages"
        assert headers["x-api-key"] == "sk-test-123" and headers["anthropic-version"] == "2026-10-21"
        assert headers["anthropic-beta"] == "server-side-fallback-2026-07-01" and body["fallbacks"] == "default"
        assert body["model"] == "claude-opus-5-5" and "temperature" not in body and "thinking" not in body
    if engine == "Gemini":
        assert headers["x-goog-api-key"] == "sk-test-123" and "gemini-2.5-flash:generateContent" in path
    if engine in ("OpenAI", "Compatible"):
        assert headers["authorization"] == "Bearer sk-test-123"
        assert path == ("/api.openai.com/v1/chat/completions" if engine == "OpenAI" else "/api.deepseek.com/v1/chat/completions")


@pytest.mark.parametrize("engine,source", [("Gemini", "api-docs.deepseek.com"), ("Anthropic", "DeepSeek API Docs"), ("OpenAI", "DeepSeek docs")])
def test_guide_research_uses_web_search(fake, tmp_path, engine, source):
    code, out = run(fake, tmp_path, engine, "sk-test-123", "", "", "research", "DeepSeek")
    assert code == 0, out
    assert "guide:DeepSeek|sources:" + source in out
    tools = json.dumps([b.get("tools") for _, _, b in seen])
    assert {"Gemini": "google_search", "Anthropic": "web_search_20260209", "OpenAI": "\"web_search\""}[engine] in tools
    if engine == "Anthropic":
        assert len(seen) == 2 and seen[1][2]["messages"][1]["role"] == "assistant"  # resumed after pause_turn


def test_rejected_key_gives_a_clear_message(fake, tmp_path):
    code, out = run(fake, tmp_path, "Anthropic", "bad-key", "", "", "rewrite", "anything at all here")
    assert code != 0 and "API key was rejected" in out


def test_in_chat_mode_builds_the_request_locally_and_contacts_nothing(fake, tmp_path):
    code, out = run(fake, tmp_path, "InChat", "unused", "", "", "rewrite", "my python script crashes on empty dates, make it skip those rows")
    assert code == 0, out
    assert seen == []  # no network call of any kind
    assert "prompt:First, rewrite my request below as a clear, well-structured prompt using Anthropic's prompting best practices for Claude:" in out
    assert "under the heading \"Improved prompt\"" in out and "under \"Missing details\", list up to 3 short questions" in out and "If a missing detail is essential, stop there and wait for my reply." in out and "under the heading \"Answer\", stating any assumptions" in out and "<request>\nmy python script crashes on empty dates" in out


def test_in_chat_mode_does_not_research_guides(fake, tmp_path):
    code, out = run(fake, tmp_path, "InChat", "unused", "", "", "research", "DeepSeek")
    assert code != 0 and "applies its own best practices" in out and seen == []
