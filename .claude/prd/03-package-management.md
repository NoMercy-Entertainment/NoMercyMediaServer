## 3. Central Package Management

### 3.1 Current State

The solution has **no centralized package management**. No `Directory.Packages.props` or `Directory.Build.props` exists.

**Package audit results:**
- **114 PackageReference entries** across **19 projects**
- Heavy duplication of common packages
- No version mismatches currently, but high risk of drift
- Several pre-release/dev packages in use

### 3.2 Most Duplicated Packages

| Package | Projects Using It | Current Version |
|---------|-------------------|-----------------|
| Newtonsoft.Json | 19 | 13.0.3 |
| Castle.Core | 13 | 5.1.1 |
| Microsoft.EntityFrameworkCore | 8 | 9.0.1 |
| xunit | 7 | 2.9.3 |
| Serilog | 6 | 4.3.1-dev-02373 |
| Microsoft.NET.Test.Sdk | 5 | 17.12.0 |
| FluentAssertions | 5 | 7.0.0 |
| Moq | 4 | 4.20.72 |
| xunit.runner.visualstudio | 4 | 2.8.2 |
| coverlet.collector | 4 | 6.0.4 |

### 3.3 Pre-Release Packages (Review Required)

| Package | Version | Action |
|---------|---------|--------|
| Serilog | 4.3.1-dev-02373 | Evaluate stable release or pin |
| Humanizer.Core | 3.0.0-beta.96 | Evaluate stable release or pin |

### 3.4 What is Central Package Management (CPM)?

CPM uses a single `Directory.Packages.props` file at the solution root to define all NuGet package versions. Individual `.csproj` files reference packages **without specifying versions** — the version comes from the central file.

**Benefits:**
- **Single source of truth** for package versions
- **No version drift** between projects
- **Easier upgrades** — update one file, all projects get the new version
- **Audit-friendly** — all dependencies visible in one place

### 3.5 Implementation Plan

**Step 1: Create `Directory.Packages.props`**
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Core -->
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.1" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.1" />

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="FluentAssertions" Version="7.0.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />

    <!-- Logging -->
    <PackageVersion Include="Serilog" Version="4.3.1-dev-02373" />

    <!-- ... all other packages -->
  </ItemGroup>
</Project>
```

**Step 2: Update every `.csproj` file**
```xml
<!-- BEFORE -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />

<!-- AFTER -->
<PackageReference Include="Newtonsoft.Json" />
```

**Step 3: Verify build**
```bash
dotnet restore
dotnet build
dotnet test
```

### 3.6 Migration Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| CPM-01 | Create `Directory.Packages.props` with all 114 package references | Medium |
| CPM-02 | Remove `Version` attribute from all `.csproj` PackageReference entries | Medium |
| CPM-03 | Verify `dotnet restore` and `dotnet build` succeed | Small |
| CPM-04 | Run all existing tests to verify no regressions | Small |
| CPM-05 | Review pre-release packages (Serilog dev, Humanizer beta) and decide to pin or upgrade | Small |
| CPM-06 | Add CI check to prevent adding versioned PackageReference entries | Small |

### 3.7 Risk Assessment

| Risk | Mitigation |
|------|------------|
| Build breaks after migration | Run full build + test suite; revert single file if needed |
| Pre-release packages pinned incorrectly | Review each pre-release package individually |
| New developers add versioned references | CI lint check + `.editorconfig` rule |
| Package-specific version overrides needed | CPM supports `VersionOverride` attribute per-project |

---

