import os
import subprocess
from pathlib import Path

from test_app import run


def test_failed_preference_save_preserves_configuration_and_worker_switch_restarts(tmp_path):
    source = tmp_path / 'Worker.cs'
    source.write_text('''using System; using System.IO; using System.Reflection;
class Worker { static void Main() { while (Console.ReadLine() != null)
Console.WriteLine("{\\"text\\":\\"" + Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) + "\\",\\"turns\\":[]}"); } }
''', encoding='utf-8')
    compiler = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    workers = [tmp_path / 'First.exe', tmp_path / 'Second.exe']
    for worker in workers:
        subprocess.run([str(compiler), '/nologo', '/target:exe', '/out:' + str(worker), str(source)], check=True, capture_output=True)
    result = run('--backend-smoke', str(tmp_path / 'data'), *map(str, workers))
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'backend-ok|atomic-preferences|failed-save-preserved|speech-runtime-switch'
