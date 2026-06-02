namespace Hex1b.Tests;

/// <summary>
/// Visual regression tests for TableWidget.
/// These tests render tables through the full Hex1bTerminal stack and compare against stored baselines.
/// </summary>
/// <remarks>
/// <para>
/// To update baselines, set the UPDATE_BASELINES environment variable to "1":
/// <code>UPDATE_BASELINES=1 dotnet test --filter "TableVisualRegressionTests"</code>
/// </para>
/// <para>
/// When adding new test cases or changing table rendering, baselines will be automatically
/// created on first run. Review the generated baseline files before committing.
/// </para>
/// </remarks>
[TestClass]
public class TableVisualRegressionTests
{
    private static bool ShouldUpdateBaselines => 
        Environment.GetEnvironmentVariable("UPDATE_BASELINES") == "1";
    
    #region Structure Tests
    
    [TestMethod]
    [DynamicData(nameof(TableVisualTestCases.StructureCases), typeof(TableVisualTestCases))]
    public async Task Structure_MatchesBaseline(TableVisualTestCase testCase)
    {
        var result = await TableVisualTestHelper.RenderAndCompareAsync(
            testCase, 
            ShouldUpdateBaselines,
            TestContext.Current.CancellationToken);
        
        if (result != null)
        {
            // Output for debugging
            var (_, text) = await TableVisualTestHelper.RenderTableAsync(testCase, TestContext.Current.CancellationToken);
            TestContext.Current?.WriteLine("Rendered output:");
            TestContext.Current?.WriteLine(text);
            
            if (result.StartsWith("BASELINE CREATED"))
            {
                // Skip test on first run when baseline is created
                Assert.Fail(result);
            }
            
            Assert.Fail(result);
        }
    }
    
    #endregion
    
    #region Selection Tests
    
    [TestMethod]
    [DynamicData(nameof(TableVisualTestCases.SelectionCases), typeof(TableVisualTestCases))]
    public async Task Selection_MatchesBaseline(TableVisualTestCase testCase)
    {
        var result = await TableVisualTestHelper.RenderAndCompareAsync(
            testCase, 
            ShouldUpdateBaselines,
            TestContext.Current.CancellationToken);
        
        if (result != null)
        {
            var (_, text) = await TableVisualTestHelper.RenderTableAsync(testCase, TestContext.Current.CancellationToken);
            TestContext.Current?.WriteLine("Rendered output:");
            TestContext.Current?.WriteLine(text);
            Assert.Fail(result);
        }
    }
    
    #endregion
    
    #region Async Loading Tests
    
    [TestMethod]
    [DynamicData(nameof(TableVisualTestCases.AsyncCases), typeof(TableVisualTestCases))]
    public async Task Async_MatchesBaseline(TableVisualTestCase testCase)
    {
        // Note: Async tests use sync data for baseline capture.
        // Loading state tests would need mock data sources with controlled timing.
        var result = await TableVisualTestHelper.RenderAndCompareAsync(
            testCase, 
            ShouldUpdateBaselines,
            TestContext.Current.CancellationToken);
        
        if (result != null)
        {
            var (_, text) = await TableVisualTestHelper.RenderTableAsync(testCase, TestContext.Current.CancellationToken);
            TestContext.Current?.WriteLine("Rendered output:");
            TestContext.Current?.WriteLine(text);
            Assert.Fail(result);
        }
    }
    
    #endregion
    
    #region Row Focus Tests
    
    [TestMethod]
    [DynamicData(nameof(TableVisualTestCases.FocusCases), typeof(TableVisualTestCases))]
    public async Task Focus_MatchesBaseline(TableVisualTestCase testCase)
    {
        var result = await TableVisualTestHelper.RenderAndCompareAsync(
            testCase, 
            ShouldUpdateBaselines,
            TestContext.Current.CancellationToken);
        
        if (result != null)
        {
            var (_, text) = await TableVisualTestHelper.RenderTableAsync(testCase, TestContext.Current.CancellationToken);
            TestContext.Current?.WriteLine("Rendered output:");
            TestContext.Current?.WriteLine(text);
            Assert.Fail(result);
        }
    }
    
    #endregion
    
    #region Table Focus Indicator Tests
    
    [TestMethod]
    [DynamicData(nameof(TableVisualTestCases.TableFocusCases), typeof(TableVisualTestCases))]
    public async Task TableFocus_MatchesBaseline(TableVisualTestCase testCase)
    {
        var result = await TableVisualTestHelper.RenderAndCompareAsync(
            testCase, 
            ShouldUpdateBaselines,
            TestContext.Current.CancellationToken);
        
        if (result != null)
        {
            var (_, text) = await TableVisualTestHelper.RenderTableAsync(testCase, TestContext.Current.CancellationToken);
            TestContext.Current?.WriteLine("Rendered output:");
            TestContext.Current?.WriteLine(text);
            Assert.Fail(result);
        }
    }
    
    #endregion
}
