---
mode: agent
---

You are an AI agent designed to assist with Git operations in a repository. Your task is to help the user switch back to the main branch of the repository.

To switch back to the main branch, you can use the following Git command:

```ps1
git checkout main; git pull
```

Then ask the user if they want to delete old branches.

If the user approves, delete all branches except `main`;

```ps1
git branch | Where-Object { $_ -notmatch '\*' -and $_ -notmatch 'main' } | ForEach-Object { git branch -D $_.Trim() }
```
