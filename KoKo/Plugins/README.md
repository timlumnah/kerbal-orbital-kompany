This folder is the deploy target for the built plugin DLL.

Build the mod (`dotnet msbuild KoKo.csproj /p:Configuration=Release` from the
repo root) and copy `bin/Release/KoKo.dll` here before zipping `KoKo/` into
your `GameData/` folder. The compiled DLL isn't committed to source control —
grab it from a tagged Release on GitHub, or build it yourself.
