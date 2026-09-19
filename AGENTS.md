## Build and publish

```powershell
dotnet build shareman.csproj
.\build_release.ps1
```

- Run all dotnet commands sequentially with `--disable-build-servers -m:1`.
- Never use `Start-Process -Wait` when invoking `dotnet` in background terminals; MSBuild worker nodes inherit redirected pipes and hang the terminal.
- Use `.\build_release.ps1` to publish the single-file Release build.
- If a build hangs or locks files, run `dotnet build-server shutdown` and retry once.