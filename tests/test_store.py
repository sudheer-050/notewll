"""Verify the built Store artifact, not the developer's installed speech engine."""
import pytest
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]


def test_store_package_identity_payload_and_update_channel():
    if not (ROOT / 'dist/Notewll-Store-build.json').is_file():
        pytest.skip('Build an MSIX with scripts/build_store.ps1 to run Store packaging checks')
    build = json.loads((ROOT / 'dist/Notewll-Store-build.json').read_text(encoding='utf-8-sig'))
    identity = json.loads((ROOT / 'store/identity.json').read_text())
    package = Path(build['Package'])
    assert hashlib.sha256(package.read_bytes()).hexdigest().upper() == build['SHA256']
    with zipfile.ZipFile(package) as archive:
        manifest = ET.fromstring(archive.read('AppxManifest.xml'))
        ns = {'a': 'http://schemas.microsoft.com/appx/manifest/foundation/windows10',
              'd': 'http://schemas.microsoft.com/appx/manifest/desktop/windows10'}
        app_identity = manifest.find('a:Identity', ns)
        assert app_identity.attrib['Name'] == identity['Name']
        assert app_identity.attrib['Publisher'] == identity['Publisher']
        assert app_identity.attrib['Version'] == build['Version']
        assert app_identity.attrib['ProcessorArchitecture'] == 'x64'
        startup = manifest.find('.//d:StartupTask', ns)
        assert startup.attrib['Enabled'] == 'false'
        assert startup.attrib['TaskId'] == 'NotewllStartup'
        required = {'Notewll.exe', 'NotewllStartup.exe', 'native/libvosk.dll',
                    'native/libgcc_s_seh-1.dll', 'native/libstdc++-6.dll',
                    'native/libwinpthread-1.dll', 'model-small-en-us/am/final.mdl',
                    'licenses/Apache-2.0.txt', 'licenses/GCC-GPL-3.0.txt',
                    'licenses/GCC-Runtime-Library-Exception.txt',
                    'licenses/MinGW-w64-COPYING.txt', 'PRIVACY.md'}
        # MSIX uses OPC URI escaping (for example libstdc++ becomes libstdc%2B%2B).
        assert required <= {unquote(name) for name in archive.namelist()}
        assert archive.read('PRIVACY.md') == (ROOT / 'store/PRIVACY.md').read_bytes()
        assert not any(name.lower().endswith(('.pfx', '.p12', '.wav', 'config.json')) for name in archive.namelist())
        staged = Path(build['Staging']) / 'Notewll.exe'
        assert archive.read('Notewll.exe') == staged.read_bytes()
    info = subprocess.run([str(staged), '--distribution-info'], capture_output=True, text=True, timeout=15)
    assert info.returncode == 0
    assert info.stdout == 'store|updates=MicrosoftStore|startup=StartupTask'
    speech = subprocess.run([str(staged), '--installed-speech-check'], capture_output=True, text=True, timeout=90)
    assert speech.returncode == 0, speech.stderr
    assert speech.stdout == 'speech-ready|offline=true'
