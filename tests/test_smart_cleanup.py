from test_app import run


def test_smart_cleanup_rewrites_only_supported_repairs_and_bounds_release():
    result = run('--smart-cleanup-smoke')
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'smart-cleanup-ok|general-restarts|grammar|protected-facts|bounded-release|no-stale-edit|verbatim'


def test_general_repeated_prefix_cleanup_works_without_a_model():
    result = run('--format-text', 'Standard', 'I just wanted to I just wanted to ask about the report')
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'I just wanted to ask about the report.'
