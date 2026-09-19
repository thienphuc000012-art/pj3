"""Apply the inventory redesign without replacing unrelated prefab edits."""
from pathlib import Path
import re
import build_prefabs as generator

ROOT = generator.ROOT
target = ROOT / 'Assets/Resources/Adventure/UI/AdventureCanvas.prefab'
generator.ROOT = ROOT / 'Library/CanvasInventoryMigration'
import sys
sys.argv.append('--replace-generated')
generated, _, _ = generator.build_adventure()

def parse(text):
    docs = dict((int(m[1]), m[0]) for m in re.finditer(r'^--- !u!\d+ &(\d+)\n[\s\S]*?(?=^--- !u!|\Z)', text, re.M))
    names, parents, rects, owners = {}, {}, {}, {}
    for fid, doc in docs.items():
        owner = re.search(r'^  m_GameObject: \{fileID: (\d+)\}', doc, re.M)
        owners[fid] = int(owner[1]) if owner else fid
        if doc.startswith('--- !u!1 '):
            names[fid] = re.search(r'^  m_Name: (.*)', doc, re.M)[1]
        if doc.startswith('--- !u!224 '):
            rects[fid] = owners[fid]
            parents[owners[fid]] = int(re.search(r'm_Father: \{fileID: (\d+)\}', doc)[1])
    def path(go):
        parent = rects.get(parents.get(go))
        return path(parent) + '/' + names[go] if parent else names[go]
    return docs, owners, {go:path(go) for go in names}

old, old_owners, old_paths = parse(target.read_text(encoding='utf-8-sig'))
new, new_owners, new_paths = parse(generated.read_text(encoding='utf-8-sig'))
scopes = ['Menu/Party', 'HUD/Hints']
def affected(path):
    return any(path == 'AdventureCanvas/' + s or path.startswith('AdventureCanvas/' + s + '/') for s in scopes)
old_ids_by_path = {p:i for i,p in old_paths.items()}
for fid, path in new_paths.items():
    if not affected(path) and path in old_ids_by_path and old_ids_by_path[path] != fid:
        raise RuntimeError('Prefab IDs changed; refusing unsafe migration: ' + path)
kept = {i:d for i,d in old.items() if not affected(old_paths.get(old_owners[i], ''))}
for i, doc in new.items():
    if affected(new_paths.get(new_owners[i], '')):
        if i in kept: raise RuntimeError('Unexpected fileID collision')
        kept[i] = doc
text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(kept.values())
ids = set(kept)
for match in re.finditer(r'\{fileID: (\d+)\}', text):
    assert int(match[1]) == 0 or int(match[1]) in ids, 'Dangling local reference: ' + match[1]
target.write_text(text, encoding='utf-8')
print('Updated party, inventory and member pickers; preserved other prefab objects.')
