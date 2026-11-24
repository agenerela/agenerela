# Prints every GitHub number on slides 15, 16, 19 and 20 from data/*.json (run fetch_gh.py first).
import os
D = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'data')
os.makedirs(D, exist_ok=True)
import json, collections as C
prs = json.load(open(os.path.join(D, 'prs.json'))); iss = json.load(open(os.path.join(D, 'issues.json')))
lg = lambda a: (a or {}).get('login', 'ghost')
print('PR states', C.Counter(p['state'] for p in prs))
m = [p for p in prs if p['state'] == 'MERGED']
print('merged by base', C.Counter(p['baseRefName'] for p in m))
print('merged authors', C.Counter(lg(p['author']) for p in m))
print('merged-by', C.Counter(lg(p['mergedBy']) for p in m))
print('all PR authors', C.Counter(lg(p['author']) for p in prs))
rv = C.Counter(); rvprs = C.defaultdict(set); st = C.Counter()
for p in prs:
    for r in p['reviews']['nodes']:
        a = lg(r['author'])
        if a != lg(p['author']): rv[a] += 1; rvprs[a].add(p['number']); st[(a, r['state'])] += 1
print('reviews by others (count)', rv)
print('PRs reviewed', {k: len(v) for k, v in rvprs.items()})
print('review states', st)
withrev = sum(1 for p in m if any(lg(r['author']) != lg(p['author']) for r in p['reviews']['nodes']))
print('merged PRs with a review by someone else', withrev, 'of', len(m))
print('issue states', C.Counter((i['state'], i['stateReason']) for i in iss))
cl = [i for i in iss if i['state'] == 'CLOSED' and i['stateReason'] == 'COMPLETED']
ac = C.Counter()
for i in cl:
    for a in i['assignees']['nodes'] or [{'login': '(none)'}]: ac[a['login']] += 1
print('completed issues by assignee', ac)
print('issue authors', C.Counter(lg(i['author']) for i in iss))
print('milestones', C.Counter((i['milestone'] or {}).get('title') for i in iss))
print('first PR', min(p['createdAt'] for p in prs), 'last merged', max(p['mergedAt'] for p in m))
lab = C.Counter(l['name'] for i in iss for l in i['labels']['nodes']); print(lab)
