# Cutting a release + submitting to CKAN

## 1. Build a release zip

```bash
dotnet msbuild KoKo.csproj /p:Configuration=Release
cp bin/Release/KoKo.dll KoKo/Plugins/
zip -r KoKo-v0.1.0.zip KoKo/
```

The zip must contain a top-level `KoKo/` folder (matching `install.find` in
`KoKo.netkan`) so CKAN — and manual installers — can drop it straight into
`GameData/`.

## 2. Tag a GitHub Release

```bash
git tag v0.1.0
git push origin v0.1.0
gh release create v0.1.0 KoKo-v0.1.0.zip --title "v0.1.0" --notes "First public release"
```

CKAN's GitHub `$kref` watches Releases automatically — new tagged releases
get picked up without touching the `.netkan` again.

## 3. Submit to CKAN

`KoKo.netkan` in this folder is the draft submission. To actually add KoKo to
the index:

1. Fork [KSP-CKAN/NetKAN](https://github.com/KSP-CKAN/NetKAN).
2. Add `KoKo.netkan` under `NetKAN/`.
3. Validate locally if possible (`netkan.exe --verbose KoKo.netkan`).
4. Open a PR. CI runs the schema/metadata checks automatically.

See the [CKAN wiki: Adding a mod to the CKAN](https://github.com/KSP-CKAN/CKAN/wiki/Adding-a-mod-to-the-CKAN)
for the full process.

## Bonus: Kitten Space Agency

KSA runs its own NetKAN-style indexer at
[KSAModding/KSA-NetKAN](https://github.com/KSAModding/KSA-NetKAN) — worth a
look once KoKo has a couple of stable releases behind it, given the whole
point of this release is putting the design in front of the KSA team.
