"""Run campaign data regressions offline with the installed .NET 9 SDK."""
from pathlib import Path
import json
import re
import shutil
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / 'Library' / 'CombatValidation'
out.mkdir(parents=True, exist_ok=True)
dotnet = shutil.which('dotnet')
if not dotnet:
    raise SystemExit('Install the .NET 9 SDK to run these tests.')
sdks = subprocess.check_output([dotnet, '--list-sdks'], text=True)
choices = re.findall(r'^(9\.[\d.]+) \[(.+)\]$', sdks, re.M)
if not choices:
    raise SystemExit('The .NET 9 SDK is required.')
version, sdk_root = choices[-1]
install = Path(sdk_root).parent
refs = sorted((install / 'packs' / 'Microsoft.NETCore.App.Ref').glob('9.*/ref/net9.0'))[-1]
assembly = out / 'CampaignDataTests.dll'
args = ['/nologo', '/target:exe', '/nostdlib+', '/out:"' + str(assembly) + '"']
args += ['/reference:"' + str(p) + '"' for p in refs.glob('*.dll')]
sources = [root / 'Assets/Scripts/Adventure/CampaignConfig.cs', *Path(__file__).parent.glob('*.cs')]
args += ['"' + str(p) + '"' for p in sources]
rsp = out / 'DataTests.rsp'
rsp.write_text('\n'.join(args), encoding='utf-8')
subprocess.run([dotnet, str(Path(sdk_root) / version / 'Roslyn/bincore/csc.dll'), '@' + str(rsp)], check=True)
assembly.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net9.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '9.0.0'}}}), encoding='utf-8')
subprocess.run([dotnet, str(assembly)], check=True)
