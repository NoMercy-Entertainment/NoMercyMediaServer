## Appendix A: Testing Checklist for Database Changes

For every EF Core query modification, verify:

- [ ] Query generates expected SQL (use `.ToQueryString()` or query logging)
- [ ] `.AsNoTracking()` used for read-only queries
- [ ] No client-side evaluation warnings in EF Core logs
- [ ] Pagination applied at SQL level (LIMIT/OFFSET in query)
- [ ] Include chains limited to needed data only
- [ ] Result matches original endpoint response (snapshot comparison)
- [ ] Performance within acceptable bounds (benchmark before/after)
- [ ] Concurrent access works without deadlocks
- [ ] Transaction behavior correct for write operations

## Appendix B: API Endpoint Testing Template

```csharp
[Fact]
public async Task GetEndpoint_ReturnsExpectedData()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", TestToken);

    // Act
    var response = await client.GetAsync("/api/v1/endpoint");

    // Assert
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadFromJsonAsync<ExpectedDto>();
    Assert.NotNull(content);
    // Verify specific fields...
}
```

## Appendix C: Cross-Platform Build Matrix

| Platform | Runtime | Service Type | Tray UI | Package Format |
|----------|---------|-------------|---------|---------------|
| Windows x64 | win-x64 | Windows Service | Avalonia | MSI/exe |
| Windows ARM64 | win-arm64 | Windows Service | Avalonia | MSI/exe |
| macOS x64 | osx-x64 | LaunchAgent | Avalonia | .app bundle |
| macOS ARM64 | osx-arm64 | LaunchAgent | Avalonia | .app bundle |
| Linux x64 | linux-x64 | systemd | Avalonia | DEB/RPM/AppImage |
| Linux ARM64 | linux-arm64 | systemd | Avalonia | DEB/RPM/AppImage |
| Linux Server | linux-x64 | systemd | None (CLI only) | DEB/RPM/Docker |
