from test_app import run


def test_raw_and_modified_modes_preserve_words_and_ignore_retired_setting():
    result = run('--smart-cleanup-smoke')
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'text-modes-ok|raw-unchanged|modified-cleanup|protected-facts|legacy-setting-ignored'


def test_general_repeated_prefix_cleanup_works_without_a_model():
    result = run('--format-text', 'Standard', 'I just wanted to I just wanted to ask about the report')
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'I just wanted to ask about the report.'
