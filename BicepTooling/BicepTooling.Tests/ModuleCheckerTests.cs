namespace BicepTooling.Tests;

using BicepTooling.Lexer;
using BicepTooling.Parser;
using BicepTooling.Semantic;

public class ModuleCheckerTests
{
    private static ModuleChecker Setup(
        string mainSource,
        string? moduleContent = null,
        string moduleFile = "network.bicep")
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bicep_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        if (moduleContent != null)
            File.WriteAllText(Path.Combine(dir, moduleFile), moduleContent);

        var ast     = new Parser(new Lexer(mainSource).Tokenize()).ParseCompilationUnit();
        var checker = new ModuleChecker(dir);
        checker.Check(ast);
        return checker;
    }

    // ── BCP008 MODULE FILE NOT FOUND ──────────────────────────

    [Fact]
    public void BCP008_ModuleFileMissing_ReportsError()
    {
        var main = "module net './network.bicep' = { name: 'x' params: { } }";
        var checker = Setup(main); // no file written
        Assert.Contains(checker.Issues, d => d.Code == "BCP008");
    }

    [Fact]
    public void BCP008_ModuleFileExists_NoError()
    {
        var module = "param location string = 'eastus'";
        var main   = "module net './network.bicep' = { name: 'x' params: { } }";
        var checker = Setup(main, module);
        Assert.DoesNotContain(checker.Issues, d => d.Code == "BCP008");
    }

    // ── BCP009 UNKNOWN PARAMETER ──────────────────────────────

    [Fact]
    public void BCP009_UnknownParam_ReportsError()
    {
        var module = "param location string";
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { location: 'eastus' typoParam: 'x' } }";
        var checker = Setup(main, module);
        Assert.Contains(checker.Issues, d => d.Code == "BCP009");
    }

    [Fact]
    public void BCP009_AllParamsKnown_NoError()
    {
        var module = "param location string\nparam vnetName string";
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { location: 'eastus' vnetName: 'myVnet' } }";
        var checker = Setup(main, module);
        Assert.DoesNotContain(checker.Issues, d => d.Code == "BCP009");
    }

    // ── BCP010 MISSING REQUIRED PARAMETER ────────────────────

    [Fact]
    public void BCP010_RequiredParamNotPassed_ReportsError()
    {
        var module = "param location string\nparam vnetName string"; // both required
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { location: 'eastus' } }"; // vnetName missing
        var checker = Setup(main, module);
        Assert.Contains(checker.Issues, d => d.Code == "BCP010");
    }

    [Fact]
    public void BCP010_OptionalParamNotPassed_NoError()
    {
        var module = "param location string = 'eastus'"; // has default — optional
        var main   = "module net './network.bicep' = { name: 'x' params: { } }";
        var checker = Setup(main, module);
        Assert.DoesNotContain(checker.Issues, d => d.Code == "BCP010");
    }

    [Fact]
    public void BCP010_RequiredParamProvided_NoError()
    {
        var module = "param location string";
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { location: 'eastus' } }";
        var checker = Setup(main, module);
        Assert.DoesNotContain(checker.Issues, d => d.Code == "BCP010");
    }

    // ── BCP011 TYPE MISMATCH ──────────────────────────────────

    [Fact]
    public void BCP011_IntParamPassedString_ReportsWarning()
    {
        var module = "param count int";
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { count: 'notanumber' } }";
        var checker = Setup(main, module);
        Assert.Contains(checker.Issues, d => d.Code == "BCP011");
    }

    [Fact]
    public void BCP011_StringParamPassedInt_ReportsWarning()
    {
        var module = "param name string";
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { name: 42 } }";
        var checker = Setup(main, module);
        Assert.Contains(checker.Issues, d => d.Code == "BCP011");
    }

    [Fact]
    public void BCP011_CorrectTypes_NoWarning()
    {
        var module = "param location string\nparam count int\nparam enabled bool";
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { location: 'eastus' count: 3 enabled: true } }";
        var checker = Setup(main, module);
        Assert.DoesNotContain(checker.Issues, d => d.Code == "BCP011");
    }

    // ── CLEAN MODULE — NO ISSUES ──────────────────────────────

    [Fact]
    public void CleanModule_NoIssues()
    {
        var module = "param location string\nparam vnetName string = 'default'";
        var main   = "module net './network.bicep' = { name: 'x' " +
                     "params: { location: 'eastus' } }";
        var checker = Setup(main, module);
        Assert.Empty(checker.Issues);
    }
}
