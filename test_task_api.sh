#!/bin/bash
BASE_URL="${MASTER_URL:-http://localhost:8080}"

echo "SimCluster Task API Test"
echo "Master URL: $BASE_URL"

echo -e "\nPing Master..."
curl -s "$BASE_URL/api/master/ping"

echo -e "\n\nWorkers:"
curl -s "$BASE_URL/api/master/workers" | python3 -m json.tool 2>/dev/null || curl -s "$BASE_URL/api/master/workers"

echo -e "\n\nSubmitting 10 tasks..."
for i in $(seq 1 10); do
  DURATION=$((RANDOM % 4000 + 1000))
  PRIORITY=$((RANDOM % 3))
  curl -s -o /dev/null -X POST "$BASE_URL/api/task" \
    -H "Content-Type: application/json" \
    -d "{\"name\":\"Test-$i\",\"durationMs\":$DURATION,\"priority\":$PRIORITY}"
done
echo "10 tasks submitted"

echo -e "\nWaiting for completion..."
sleep 8

echo -e "\nStats:"
curl -s "$BASE_URL/api/task/stats" | python3 -m json.tool 2>/dev/null || curl -s "$BASE_URL/api/task/stats"

echo -e "\n\nTask distribution:"
curl -s "$BASE_URL/api/task" | python3 -c "
import json,sys,collections
data=json.load(sys.stdin)
c=collections.Counter(t.get('assignedWorkerId','?') for t in data.get('tasks',[]))
for wid,cnt in c.most_common(): print(f'  {wid}: {cnt}')
" 2>/dev/null || echo "(install python3 for distribution summary)"