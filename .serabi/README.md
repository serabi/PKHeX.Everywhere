# Serabi fork

This fork keeps upstream's application and submodule pins intact. The current
branch adds a plan and Git push protection. It contains no save-reader
implementation.

- Fork: https://github.com/serabi/PKHeX.Everywhere
- Upstream: https://github.com/arleypadua/PKHeX.Everywhere
- Implementation plan: [PLAN.md](PLAN.md)
- Planning branch: `serabi/read-only-import-plan`
- Starting upstream commit: `6373c4c7de5405cdeafc72ecbe826390c4622b17`

## Push protection

The protected checkout pushes to `origin`, which points at the Serabi fork.
The `upstream` remote fetches from the original project and has an unusable
push URL. A pre-push hook allows only the Serabi fork's HTTPS and SSH URLs.
It also rejects a destination named `upstream`.

The installed hook lives inside Git's directory, so switching to upstream's
unchanged `main` branch keeps the protection active. The tracked hook here
is the source copy. Reinstall it after changing that copy.

These settings protect this local checkout. GitHub forks do not carry local
hooks or remote configuration into new clones. Git hooks can also be bypassed
by an explicit override. This is an accident guard, not a GitHub permission
rule. At setup, the authenticated account had no push permission on upstream.

## Protect another clone

Run these commands from this fork on a branch containing `.serabi/`.
Check `git config --show-origin --get core.hooksPath` first. If the checkout
already uses hooks, combine the checks with its existing pre-push hook rather
than replacing its hooks configuration.

```sh
git remote -v
# If upstream is absent, add it once:
git remote add upstream https://github.com/arleypadua/PKHeX.Everywhere.git
git remote set-url --push upstream disabled://upstream-push-blocked
git config --local remote.pushDefault origin
git config --local branch.main.pushRemote origin
git config --local push.default simple
gh repo set-default serabi/PKHeX.Everywhere

hook_dir="$(git rev-parse --absolute-git-dir)/serabi-hooks"
mkdir -p "$hook_dir"
cp .serabi/hooks/pre-push "$hook_dir/pre-push"
chmod +x "$hook_dir/pre-push"
git config --local core.hooksPath "$hook_dir"
```

Verify that `origin` points at `serabi/PKHeX.Everywhere` before pushing.
`git push --dry-run upstream HEAD` must fail on the disabled push URL.
Run the installed hook directly with the upstream URL to check the second
guard without sending any refs:

```sh
"$hook_dir/pre-push" direct https://github.com/arleypadua/PKHeX.Everywhere.git
```

That command must exit with status 1. The same command with the fork's URL
must exit with status 0.

## Update from upstream

Keep `main` as an unchanged copy of upstream. Keep fork documentation,
the generic API addition, the reader package, and the website integration
in separate changes. Do not rename upstream projects or rewrite their
existing package versions just to identify the fork.

With a clean checkout, update `main` and merge it into each active branch:

```sh
git fetch upstream
git switch main
git merge --ff-only upstream/main
git push origin main
git switch serabi/read-only-import-plan
git merge main
git push origin HEAD
```

For future implementation branches, run the checks listed in the plan after
each update. Record the upstream SHA and submodule pins used by each reader
release. Keep the hook source on maintained fork branches and its installed
copy in Git's directory.

Do not publish to upstream's npm packages, deploy its sites, or open upstream
PRs or issues as part of this planning task. Future fork releases need a
separate release destination and credentials.
