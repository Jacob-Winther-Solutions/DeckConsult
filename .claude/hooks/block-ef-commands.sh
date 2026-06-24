#!/usr/bin/env bash
# Blocks EF Core migration and database update commands.
# These must always be run manually — see CLAUDE.md rules.

input=$(cat)

if printf '%s' "$input" | grep -qE 'dotnet[[:space:]]+ef[[:space:]]+(migrations[[:space:]]+add|database[[:space:]]+update)'; then
    printf '{"decision":"block","reason":"EF Core migrations must be run manually by you. This is a hard project rule (CLAUDE.md). I will tell you which command to run instead of executing it myself."}'
    exit 2
fi
