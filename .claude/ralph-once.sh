#!/bin/bash

claude --dangerously-skip-permissions "@PRD.md @progress.md \
1. Read the PRD and progress file. \
2. Find the next unchecked task ([ ]) and implement it. \
3. Run dotnet build && dotnet test — both must pass. \
4. Mark the task [x] in PRD.md and update the 'Next up' line. \
5. Append what you did to progress.md. \
6. Commit your changes. \
Do not waste tokens on useless comments or random unrelated files. \
ONLY DO ONE TASK AT A TIME."
