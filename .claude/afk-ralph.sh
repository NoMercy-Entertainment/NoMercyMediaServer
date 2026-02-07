#!/bin/bash
set -e

if [ -z "$1" ]; then
  echo "Usage: $0 <iterations>"
  exit 1
fi

for ((i=1; i<=$1; i++)); do
  result=$(claude --dangerously-skip-permissions -p "@PRD.md @progress.md \
  1. Find the next unchecked task ([ ]) and implement it. \
  2. Run dotnet build && dotnet test — both must pass. \
  3. Mark the task [x] in PRD.md and update the 'Next up' line. \
  4. Append your progress to progress.md. \
  5. Commit your changes. \
  Do not waste tokens on useless comments or random unrelated files. \
  ONLY WORK ON A SINGLE TASK. \
  If the PRD is complete, output <promise>COMPLETE</promise>.")

  echo "$result"

  if [[ "$result" == *"<promise>COMPLETE</promise>"* ]]; then
    echo "PRD complete after $i iterations."
    exit 0
  fi
done
