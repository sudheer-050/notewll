import pytest
from test_app import clean, run


@pytest.mark.parametrize('spoken,expected', [
    ("Can you write me what's the what's the best restaurant near me?", "Can you write me what's the best restaurant near me?"),
    ("Can you write me no what's the what's the best restaurant near me?", "Can you write me no what's the best restaurant near me?"),
    ("What's the what's the best restaurant near me?", "What's the best restaurant near me?"),
    ("What\u2019s the what\u2019s the best restaurant near me?", "What\u2019s the best restaurant near me?"),
    ("What\u2019s the what's the best restaurant near me?", "What's the best restaurant near me?"),
    ("What's the, what's the best restaurant near me?", "What's the best restaurant near me?"),
    ("What's the what's the what's the best restaurant near me?", "What's the best restaurant near me?"),
    ("Where's the where's the nearest station?", "Where's the nearest station?"),

    ('Hey, how are you? I just wanted to talk to you about what are what what about the king?', 'Hey, how are you? I just wanted to talk to you about the king?'),
    ('We talked about what are what what about not sending Alex 42 dollars on Friday.', 'We talked about not sending Alex 42 dollars on Friday.'),
    ('We talked about what is what about the deadline.', 'We talked about the deadline.'),
    ('Can you list what are what updates you have done?', 'Can you list what updates you have done?'),
    ('Can you list what are, what updates you have done?', 'Can you list what updates you have done?'),
    ('Please show me which are which changes were applied.', 'Please show me which changes were applied.'),
    ('Tell me what was what date we agreed on.', 'Tell me what date we agreed on.'),
    ('Can you can you list the updates?', 'Can you list the updates?'),
    ('I want to, I want to review the report.', 'I want to review the report.'),
])
def test_spoken_request_restarts_are_removed(spoken, expected):
    assert clean(spoken) == expected
    result = run('--format-text', 'Standard', spoken)
    assert result.returncode == 0, result.stderr
    assert result.stdout == expected


@pytest.mark.parametrize('text', [
    'We talked about what is known about the king.',
    'What about what the king said?',
    'We talked about what are the facts. What about the king?',
    "What's the best restaurant? What's the price?",
    "What's the meaning of 'what's the'?",
    "What's the best restaurant near Alex at 3.30?",
    'He said that that was fine.',

    'She had had enough.',
    'What is what makes this work?',
    'Tell me what is what matters most.',
    'Show me what is what he said.',
    'Can you list what updates you have not done?',
    'Can you list what updates cost 42 dollars?',
    'Can you list what updates Alex made on Friday?',
    'Can you? Can you list the updates?',
    'I want to keep this. I want to keep that.',
])
def test_restart_cleanup_preserves_meaning(text):
    assert clean(text) == text


def test_code_mode_keeps_spoken_restarts_literal():
    spoken = 'Can you list what are what updates you have done?'
    result = run('--format-text', 'Code', spoken)
    assert result.returncode == 0, result.stderr
    assert result.stdout == spoken

