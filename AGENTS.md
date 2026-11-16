# Project versioning

For every completed change request that fixes a bug or adds a feature, increment
the project version once. If a request includes multiple changes, use the
highest applicable increment:

- Bug fix: increment patch, e.g. `0.10.2` to `0.10.3`.
- Backward-compatible feature: increment minor and reset patch, e.g. `0.10.2`
  to `0.11.0`.
- Breaking change: increment major and reset minor and patch, e.g. `0.10.2`
  to `1.0.0`.

Documentation-only, test-only, and other changes that do not alter product
behavior do not require a version increment.

Keep the version in the `MelonInfo` attribute in `ScreenGrab.cs` and the
`Version` property in `BigscreenScreenGrab.csproj` identical. The SDK generates
assembly, file, and informational version attributes from the project version;
do not edit generated files under `obj`. If these sources disagree, use the
`MelonInfo` version as the starting point for the next increment, then sync both.
Build the project and verify the generated version attributes match the new
version (assembly and file versions use a fourth `.0` component).

## Manual GitHub release

When the user asks to release this mod, use this checklist; never publish
automatically. Project: `BigscreenScreenGrab.csproj`; version source:
`ScreenGrab.cs` `MelonInfo`; asset:
`bin\Release\net6.0\BigscreenScreenGrab.dll`.

1. Check the GitHub account, repository and remote, branch, working tree,
   latest release and local and remote tags. If the repository is not set up,
   the working tree is dirty, or the branch is not synchronized with its remote,
   stop and ask how to proceed. Do not silently commit or push changes.
2. Read the version from `MelonInfo` and confirm the project `Version` matches.
   Use `v<version>` as the release tag. If that tag or release already exists,
   stop and ask; never overwrite it or retag a commit. Do not bump versions just
   to release without user approval.
3. Build the clean, pushed commit with
   `dotnet build .\BigscreenScreenGrab.csproj -c Release --no-incremental`.
   Use the documented `GameDir` override if needed. Verify the DLL's assembly
   and file versions match the release version, with a fourth `.0` component.
   Stop on build or verification failure.
4. Review the changes since the previous release and the README. Prepare
   accurate release notes covering changes, tested game and MelonLoader
   versions, dependencies, installation, and relevant limitations. Do not claim
   in-game testing unless it occurred.
5. Use `gh release create` with the `v<version>` tag, the full pushed commit
   SHA as `--target`, the release notes, and the mod DLL as an asset. Do not
   include game assemblies, secrets, or local build intermediates.
6. Verify the published release, tag, target commit, and asset. Download the
   asset to temporary storage and compare its SHA-256 hash with the built DLL.
   Report the release URL and any testing limitations.
