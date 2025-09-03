# TMDB Provider Test Suite

This directory contains comprehensive tests for the TMDB (The Movie Database) provider implementation in NoMercy Media Server.

## Test Structure

### Client Tests (`Client/`)
- **TmdbMovieClientTests.cs**: Unit tests for movie client functionality
- **TmdbMovieClientIntegrationTests.cs**: Integration tests that make real API calls
- **TmdbBaseClientTests.cs**: Tests for the base HTTP client functionality
- **TmdbPerformanceTests.cs**: Performance and load testing
- **TmdbErrorHandlingTests.cs**: Error handling and edge case testing

### Model Tests (`Models/`)
- **TmdbMovieModelsTests.cs**: Serialization/deserialization and data integrity tests

### Mock Data (`Mocks/`)
- **TmdbMovieMockData.cs**: Sample data for testing without making API calls

### Base Classes
- **TmdbTestBase.cs**: Base class providing common test setup and utilities
- **TmdbTestRunner.cs**: Test discovery and organization utilities

## Test Categories

### Unit Tests
- **Purpose**: Test individual components in isolation
- **Speed**: Fast (< 5 seconds)
- **Dependencies**: Uses mock data, no external API calls
- **Run Command**: `dotnet test --filter Category=Unit`

### Integration Tests
- **Purpose**: Test against real TMDB API
- **Speed**: Slower (may take 30+ seconds)
- **Dependencies**: Requires valid TMDB API key and internet connection
- **Run Command**: `dotnet test --filter Category=Integration`
- **Note**: May be rate-limited by TMDB API

### Performance Tests
- **Purpose**: Measure response times and throughput
- **Run Command**: `dotnet test --filter Category=Performance`

### Error Handling Tests
- **Purpose**: Verify robust behavior under failure conditions
- **Run Command**: `dotnet test --filter Category=ErrorHandling`

## Running Tests

### All Tests
```bash
dotnet test
```

### Unit Tests Only (Recommended for CI/CD)
```bash
dotnet test --filter Category!=Integration
```

### Integration Tests Only
```bash
dotnet test --filter Category=Integration
```

### Specific Test Class
```bash
dotnet test --filter ClassName=TmdbMovieClientTests
```

### With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Data

### Mock Data
The test suite includes realistic mock data based on actual TMDB API responses:
- **The Dark Knight** (ID: 155) - Primary test movie with complete data
- **Minimal Movie** (ID: 999999) - Movie with minimal required fields
- **Generated Movies** - Dynamically created test data with specified IDs

### Real API Data
Integration tests use well-known movies with stable data:
- **The Dark Knight** (ID: 155)
- **The Shawshank Redemption** (ID: 278)

## Configuration

### API Key Setup
For integration tests, ensure your TMDB API key is configured in:
- Environment variables
- Configuration files
- Or through the ApiInfo.TmdbToken property

### Test Settings
- **Performance Thresholds**: Configurable timeout values for performance tests
- **Rate Limiting**: Tests respect TMDB API rate limits
- **Retry Logic**: Automatic retry for transient failures in integration tests

## Best Practices

### Writing New Tests
1. **Inherit from TmdbTestBase** for common functionality
2. **Use descriptive naming**: `Method_Scenario_ExpectedResult`
3. **Include both positive and negative test cases**
4. **Use appropriate test categories** with `[Trait]` attributes
5. **Mock external dependencies** for unit tests
6. **Test edge cases** and error conditions

### Mock Data
1. **Use existing mock data** when possible
2. **Create realistic test data** that mirrors actual API responses
3. **Include both minimal and complete data sets**
4. **Version control mock data** for consistency

### Performance Tests
1. **Set realistic thresholds** based on expected performance
2. **Test both single and concurrent operations**
3. **Include memory usage verification**
4. **Consider rate limiting** in performance calculations

## Troubleshooting

### Common Issues

#### Integration Tests Failing
- **Check API key configuration**
- **Verify internet connectivity**
- **Check for TMDB API rate limiting**
- **Ensure test movie IDs still exist in TMDB**

#### Performance Tests Timing Out
- **Check system performance**
- **Verify network latency**
- **Adjust performance thresholds if needed**
- **Consider running tests sequentially**

#### Mock Data Issues
- **Verify JSON serialization/deserialization**
- **Check for breaking changes in model classes**
- **Ensure mock data matches current API format**

## Maintenance

### Regular Tasks
1. **Update mock data** when TMDB API changes
2. **Review performance thresholds** as system performance changes
3. **Add tests for new functionality**
4. **Clean up obsolete tests**
5. **Update integration test data** if TMDB content changes

### Dependencies
- **xUnit**: Testing framework
- **FluentAssertions**: Assertion library
- **Moq**: Mocking framework (if needed)
- **Newtonsoft.Json**: JSON serialization

## Contributing

When adding new tests:
1. Follow the existing naming conventions
2. Add appropriate test categories
3. Include both unit and integration tests for new features
4. Update this README if adding new test types or categories
5. Ensure tests are deterministic and isolated
