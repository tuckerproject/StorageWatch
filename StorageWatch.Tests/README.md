# StorageWatch.Tests

Comprehensive test suite for the StorageWatch application, covering unit tests, integration tests, and code coverage reporting.

## Test Structure

### Unit Tests (`/UnitTests`)

Pure unit tests that verify individual component behavior in isolation:

- **DiskAlertMonitorTests** - Drive scanning logic, disk status calculations
- **ConfigLoaderTests** - XML configuration parsing and validation
- **AlertSenderFactoryTests** - Alert sender instantiation logic
- **SqlReporterTests** - SQL reporter initialization

### Integration Tests (`/IntegrationTests`)

Tests that verify component interaction with real dependencies:

- **SqliteSchemaIntegrationTests** - Database schema creation and validation
- **SqlReporterIntegrationTests** - Full SQLite write operations
- **AlertSenderIntegrationTests** - Alert delivery mechanisms (with error handling)
- **NetworkReadinessTests** - DNS resolution and network availability checks

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run with Code Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Run with Detailed Coverage Report
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./TestResults/
```

### Run Specific Test Category
```bash
# Unit tests only
dotnet test --filter "FullyQualifiedName~UnitTests"

# Integration tests only
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

## Code Coverage

The project uses **Coverlet** for code coverage analysis. Coverage is configured to:

- Track all assemblies in the StorageWatch project
- Generate OpenCover format reports
- Exclude test assemblies from coverage metrics

### Coverage Thresholds

Target coverage metrics (to be enforced in CI/CD):
- **Line Coverage**: 70%+
- **Branch Coverage**: 60%+
- **Method Coverage**: 75%+

## Test Dependencies

- **xUnit** - Test framework
- **FluentAssertions** - Assertion library for readable tests
- **Moq** - Mocking framework for unit tests
- **Coverlet** - Code coverage tool
- **Microsoft.Data.Sqlite** - For integration tests with real database

## Writing New Tests

### Unit Test Example
```csharp
[Fact]
public void MyComponent_WithValidInput_ReturnsExpectedResult()
{
    // Arrange
    var component = new MyComponent();
    
    // Act
    var result = component.DoSomething();
    
    // Assert
    result.Should().Be(expectedValue);
}
```

### Integration Test Example
```csharp
[Fact]
public async Task MyIntegration_WithRealDatabase_PersistsData()
{
    // Arrange
    var connectionString = "Data Source=:memory:";
    var component = new MyComponent(connectionString);
    
    // Act
    await component.SaveDataAsync();
    
    // Assert
    var savedData = await component.LoadDataAsync();
    savedData.Should().NotBeNull();
}
```

## Continuous Integration

Tests are designed to run in CI/CD pipelines:
- All tests must pass before merge
- Code coverage reports are generated automatically
- Integration tests use in-memory databases or mocked external services

## Troubleshooting

### Tests Fail on CI but Pass Locally
- Ensure no tests depend on local file paths
- Check for time zone or date/time dependencies
- Verify all test databases use `:memory:` or temp directories

### Coverage Not Generating
```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet test /p:CollectCoverage=true
```

### Test Discovery Issues
- Ensure test methods are marked with `[Fact]` or `[Theory]`
- Verify test class is public
- Check that test project references xUnit correctly

## Future Enhancements

- Add performance/benchmarking tests
- Add end-to-end tests for the full service lifecycle
- Add mutation testing for test quality verification
- Add snapshot testing for configuration validation
