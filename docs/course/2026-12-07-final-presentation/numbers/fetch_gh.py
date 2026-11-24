# Pulls every PR (with reviews) and every issue of agenerela/agenerela into data/*.json.
# Run: python fetch_gh.py   (needs gh, signed in). data/ is not committed; re-run to refresh.
import os
D = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'data')
os.makedirs(D, exist_ok=True)
import json, subprocess
def gql(q, **v):
    args = ['gh', 'api', 'graphql', '-f', 'query=' + q]
    for k, val in v.items():
        if val is not None: args += ['-F', f'{k}={val}']
    return json.loads(subprocess.run(args, capture_output=True, text=True, check=True).stdout)
PRQ = '''query($c:String){repository(owner:"agenerela",name:"agenerela"){pullRequests(first:50,after:$c){pageInfo{hasNextPage endCursor}
nodes{number title state createdAt mergedAt baseRefName headRefName author{login} mergedBy{login} additions deletions changedFiles
reviews(first:50){nodes{author{login} state submittedAt}} comments{totalCount}}}}}'''
ISQ = '''query($c:String){repository(owner:"agenerela",name:"agenerela"){issues(first:100,after:$c){pageInfo{hasNextPage endCursor}
nodes{number title state stateReason createdAt closedAt author{login} assignees(first:10){nodes{login}} labels(first:10){nodes{name}} milestone{title}}}}}'''
for name, q, key in (('prs', PRQ, 'pullRequests'), ('issues', ISQ, 'issues')):
    out, c = [], None
    while True:
        d = gql(q, c=c)['data']['repository'][key]
        out += d['nodes']
        if not d['pageInfo']['hasNextPage']: break
        c = d['pageInfo']['endCursor']
    json.dump(out, open(os.path.join(D, f'{name}.json'), 'w'), indent=1)
    print(name, len(out))
