"""
Build game1_test.unity: take GitHub's updated game1 (environment art) as the base,
remove its conflicting functional objects (Area_Left/Right/Top, Soldier, Trader),
and transplant the local functional system (R1-R4 GameAreas, 3 InvisibleWalls,
Soldier/NPC_R2/NPC_R3 gate-NPCs) into a collision-free fileID range.

Transplantation is done by EXPLICIT fileID (not name) because several objects
share a name (3x 'InvisibleWall'). Each transplanted NPC's Conversation gets a
single "test" step so the unlock chain can be verified in the editor.
"""
import re
import sys

BASE_FILE = 'gcet_game_unity/Assets/Scenes/game1.unity'          # GitHub art base
TMPL_FILE = '_preserve_before_merge/game1.local-backup.unity'    # local functional objects
OUT_FILE  = 'gcet_game_unity/Assets/Scenes/game1_test.unity'

STEP = (
    "  steps:\n"
    "  - speakerName:\n"
    "      m_Name: soldier\n"
    "    expression:\n"
    "      m_Name: normal\n"
    "    text:\n"
    "      m_Name: test\n"
    "    action: 0\n"
    "    choices: []\n"
    "    nextStep: -1\n"
    "  repeatSteps: []"
)

# GameObject fileIDs to transplant, keyed by a stable label. Component fileIDs are
# auto-derived from each GameObject's m_Component list below.
FUNCTIONAL_GO = {
    'R1':      200000000,
    'R4':      200000010,
    'R2':      200000030,
    'Soldier': 200000020,
    'R3':      3000000100,
    'Wall1':   3000000110,
    'Wall2':   3000000120,
    'Wall3':   3000000130,
    'NPC_R2':  3000000200,
    'NPC_R3':  3000000210,
}
CONVERSATION_GUID = '35e65e4302661f84f8428024eb88eebc'  # Assembly-CSharp::Conversation script guid

# ---------- YAML document helpers ----------
def split_docs(text):
    seps = re.split(r'(\n--- !u!\d+ &\d+\n)', text)
    docs = []
    i = 1
    while i < len(seps):
        m = re.match(r'\n--- !u!(\d+) &(\d+)', seps[i])
        body = seps[i+1] if i+1 < len(seps) else ''
        if m:
            docs.append([int(m.group(1)), int(m.group(2)), body])
        i += 2
    return docs

def remap_body(body, rmap):
    def repl(m):
        n = int(m.group(1))
        return '{fileID: ' + str(rmap[n]) + '}' if n in rmap else m.group(0)
    return re.sub(r'\{fileID: (\d+)\}', repl, body)

def patch_conversation(body):
    if re.search(r'steps:\s*\[\]', body):
        return re.sub(r'steps:\s*\[\]', STEP.strip(), body)
    return re.sub(r'(m_EditorClassIdentifier: Assembly-CSharp::Conversation\s*\n)',
                   r'\1' + STEP + '\n', body)

# ---------- load ----------
base_docs = split_docs(open(BASE_FILE, encoding='utf-8').read())
tmpl_docs = split_docs(open(TMPL_FILE, encoding='utf-8').read())
tmpl_by_id = {fid: (tag, body) for tag, fid, body in tmpl_docs}

# ---------- A. Remove GitHub's conflicting functional objects ----------
def find_go_fileid(docs, name):
    for tag, fid, body in docs:
        if tag == 1 and re.search(r'^  m_Name: ' + re.escape(name) + r'\s*$', body, re.M):
            return fid
    return None

def find_components(docs, go_fid):
    for tag, fid, body in docs:
        if tag == 1 and fid == go_fid:
            return [int(x) for x in re.findall(r'component: \{fileID: (\d+)\}', body)]
    return []

remove_names = ['Area_Left', 'Area_Right', 'Area_Top', 'Soldier', 'Trader']
remove_gos = [fid for fid in (find_go_fileid(base_docs, n) for n in remove_names) if fid is not None]

remove_ids = set()
for go in remove_gos:
    remove_ids.add(go)
    for c in find_components(base_docs, go):
        remove_ids.add(c)

base_docs = [d for d in base_docs if d[1] not in remove_ids]

def scrub_refs(body, ids):
    """Drop references to removed fileIDs regardless of YAML list format.

    Handles: `- {fileID: N}` (m_Roots / m_Children list items),
    `- component: {fileID: N}` (m_Component list), and `m_Father: {fileID: N}`.
    Removing the entire line keeps surrounding indentation/structure intact.
    """
    def drop_line(m):
        return '' if int(m.group(1)) in ids else m.group(0)
    # "- {fileID: N}" and "- component: {fileID: N}" — drop the whole line.
    body = re.sub(r'\n[ \t]+- (component: )?\{fileID: (\d+)\}',
                  lambda m: '' if int(m.group(2)) in ids else m.group(0), body)
    # Single "key: {fileID: N}" style (m_Father, m_ProbeAnchor, etc.).
    body = re.sub(r'(?<=[\n\r])([ \t]+\S+: )\{fileID: (\d+)\}',
                  lambda m: ('' if int(m.group(2)) in ids else m.group(0)), body)
    return body

for d in base_docs:
    d[2] = scrub_refs(d[2], remove_ids)

def register_roots(body, new_root_ids):
    lines = body.split('\n')
    out = []
    in_roots = False
    roots_indent = None
    inserted = False
    for line in lines:
        if not inserted and re.match(r'(\s*)m_Roots:\s*$', line):
            in_roots = True
            m = re.match(r'(\s*)m_Roots:', line)
            roots_indent = m.group(1)
            out.append(line)
            # Insert the new root transform entries right after the m_Roots: line.
            for rid in new_root_ids:
                out.append(roots_indent + '- {fileID: ' + str(rid) + '}')
            inserted = True
            continue
        out.append(line)
    return '\n'.join(out)

transplanted_transform_ids = []  # filled during transplant (section B/C)

# ---------- B. Transplant by explicit fileID ----------
NEW_BASE = 9_000_000_000
next_new = [NEW_BASE]

def alloc_id():
    v = next_new[0]
    next_new[0] += 1
    return v

# Auto-derive each GO's component fileIDs from its m_Component list.
comps_of = {}
for name, go_fid in FUNCTIONAL_GO.items():
    assert go_fid in tmpl_by_id, f"{name}: GO {go_fid} not in template"
    tag, body = tmpl_by_id[go_fid]
    assert tag == 1, f"{name}: expected GameObject (tag 1), got {tag}"
    comps_of[name] = [int(x) for x in re.findall(r'component: \{fileID: (\d+)\}', body)]
    for c in comps_of[name]:
        assert c in tmpl_by_id, f"{name}: component {c} not in template"

remap = {}
for name, go_fid in FUNCTIONAL_GO.items():
    remap[go_fid] = alloc_id()
    for c in comps_of[name]:
        if c not in remap:
            remap[c] = alloc_id()

def is_conversation(fid):
    tag, body = tmpl_by_id[fid]
    return CONVERSATION_GUID in body

new_docs = []
added = set()
for name, go_fid in FUNCTIONAL_GO.items():
    if go_fid in added:
        continue
    added.add(go_fid)
    # GameObject doc
    tag, body = tmpl_by_id[go_fid]
    new_docs.append([tag, remap[go_fid], remap_body(body, remap)])
    # Component docs
    for c in comps_of[name]:
        tag, body = tmpl_by_id[c]
        body = remap_body(body, remap)
        if tag == 4:  # Transform -> this transplanted object is a root
            transplanted_transform_ids.append(remap[c])
        if is_conversation(c):
            body = patch_conversation(body)
        new_docs.append([tag, remap[c], body])

# ---------- C. Register new roots + append transplanted docs ----------
for d in base_docs:
    if 'm_Roots:' in d[2]:
        d[2] = register_roots(d[2], transplanted_transform_ids)
        break


insert_idx = len(base_docs)
for i, (tag, fid, body) in enumerate(base_docs):
    if 'SceneRoots' in body:
        insert_idx = i
        break
final_docs = base_docs[:insert_idx] + new_docs + base_docs[insert_idx:]

# ---------- D. Validate ----------
all_ids = [d[1] for d in final_docs]
dups = sorted({x for x in all_ids if all_ids.count(x) > 1})
assert not dups, f"Duplicate fileIDs! {dups}"

referenced = set()
for tag, fid, body in final_docs:
    for m in re.findall(r'\{fileID: (\d+)\}', body):
        referenced.add(int(m))
missing = referenced - set(all_ids)
missing.discard(0)
if missing:
    print(f"WARNING: references to non-existent fileIDs: {sorted(missing)[:20]}", file=sys.stderr)
else:
    print("Validation OK: all fileID references resolve.")

def count_name(docs, name):
    return sum(1 for tag, fid, body in docs
               if tag == 1 and re.search(r'^  m_Name: ' + re.escape(name) + r'\s*$', body, re.M))
for check in ['R1', 'R2', 'R3', 'R4', 'Soldier', 'NPC_R2', 'NPC_R3']:
    assert count_name(final_docs, check) == 1, f"Missing/duplicate {check}"
assert count_name(final_docs, 'InvisibleWall') == 3, f"InvisibleWall count != 3"
print(f"OK: R1-R4, 3 InvisibleWalls, Soldier/NPC_R2/NPC_R3 present. "
      f"FileIDs remapped into range {NEW_BASE}+.")

# ---------- E. Serialize ----------
with open(OUT_FILE, 'w', encoding='utf-8', newline='') as f:
    f.write('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:')
    for tag, fid, body in final_docs:
        # Unity separates top-level docs with "\n--- !u!N &M\n"; the leading
        # newline is essential or the runs-together bodies become one giant doc.
        f.write(f'\n--- !u!{tag} &{fid}\n{body}')

print(f"Wrote {OUT_FILE} ({len(final_docs)} docs).")
