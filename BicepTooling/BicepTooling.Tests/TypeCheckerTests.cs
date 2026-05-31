namespace BicepTooling.Tests;

using BicepTooling.Lexer;
using BicepTooling.Parser;
using BicepTooling.Semantic;

public class TypeCheckerTests
{
    private static TypeChecker Check(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        var ast    = new Parser(tokens).ParseCompilationUnit();
        var symbols = new SymbolResolver().Resolve(ast);
        var checker = new TypeChecker(symbols);
        checker.Check(ast);
        return checker;
    }

    // ── BCP001 TYPE MISMATCH ──────────────────────────────────

    [Fact]
    public void BCP001_IntParam_StringDefault_ReportsError()
    {
        var checker = Check("param count int = 'hello'");
        Assert.Single(checker.Errors);
        Assert.Equal("BCP001", checker.Errors[0].Code);
    }

    [Fact]
    public void BCP001_BoolParam_IntDefault_ReportsError()
    {
        var checker = Check("param enabled bool = 42");
        Assert.Single(checker.Errors);
        Assert.Equal("BCP001", checker.Errors[0].Code);
    }

    [Fact]
    public void BCP001_StringParam_StringDefault_NoError()
    {
        var checker = Check("param location string = 'eastus'");
        Assert.Empty(checker.Errors);
        Assert.Empty(checker.Warnings);
    }

    [Fact]
    public void BCP001_IntParam_IntDefault_NoError()
    {
        var checker = Check("param count int = 42");
        Assert.Empty(checker.Errors);
        Assert.Empty(checker.Warnings);
    }

    // ── BCP005 REQUIRED PARAM ────────────────────────────────

    [Fact]
    public void BCP005_ParamNoDefault_ReportsInfo()
    {
        var checker = Check("param storageAccountName string");
        Assert.Empty(checker.Errors);
        Assert.Single(checker.Infos);
        Assert.Equal("BCP005", checker.Infos[0].Code);
    }

    [Fact]
    public void BCP005_ParamWithDefault_NoInfo()
    {
        var checker = Check("param location string = 'eastus'");
        Assert.DoesNotContain(checker.Infos, d => d.Code == "BCP005");
    }

    // ── BCP004 MISSING API VERSION ───────────────────────────

    [Fact]
    public void BCP004_ResourceMissingAtSign_ReportsError()
    {
        var checker = Check(
            "resource sa 'Microsoft.Storage/storageAccounts' = { }");
        Assert.Contains(checker.Errors, d => d.Code == "BCP004");
    }

    [Fact]
    public void BCP004_ResourceHasApiVersion_NoError()
    {
        var checker = Check(
            "resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = { }");
        Assert.DoesNotContain(checker.Errors, d => d.Code == "BCP004");
    }

    // ── BCP002 UNDEFINED REFERENCE ───────────────────────────

    [Fact]
    public void BCP002_UndeclaredIdentifierInVar_ReportsError()
    {
        var checker = Check("var x = undeclaredName");
        Assert.Contains(checker.Errors, d => d.Code == "BCP002");
    }

    [Fact]
    public void BCP002_DeclaredVarReused_NoError()
    {
        var checker = Check(
            "var storageSku = 'Standard_LRS'\nvar x = storageSku");
        Assert.DoesNotContain(checker.Errors, d => d.Code == "BCP002");
    }

    // ── FUZZY NAME SUGGESTION (Levenshtein) ──────────────────

    [Fact]
    public void FuzzySuggestion_OneCharTypo_ShowsDidYouMean()
    {
        // "storagSku" is 1 edit away from "storageSku" → suggestion shown
        var source = "var storageSku = 'Standard_LRS'\nvar x = storagSku";
        var checker = Check(source);
        var diag = checker.Errors.First(d => d.Code == "BCP002");
        Assert.Contains("Did you mean", diag.What);
        Assert.Contains("storageSku", diag.What);
    }

    [Fact]
    public void FuzzySuggestion_DistantName_NoSuggestion()
    {
        // "completelyDifferentName" is far from "storageSku" → no suggestion
        var source = "var storageSku = 'Standard_LRS'\nvar x = completelyDifferentName";
        var checker = Check(source);
        var diag = checker.Errors.First(d => d.Code == "BCP002");
        Assert.DoesNotContain("Did you mean", diag.What);
    }

    // ── BCP007 CROSS-RESOURCE MISMATCH ───────────────────────

    [Fact]
    public void BCP007_VarUsedAsResource_DotId_ReportsWarning()
    {
        // 'subnetName' is a var — accessing .id on it should warn
        var source = "var subnetName = 'mySubnet'\noutput id string = subnetName.id";
        var checker = Check(source);
        Assert.Contains(checker.Warnings, d => d.Code == "BCP007");
    }

    [Fact]
    public void BCP007_ParamUsedAsResource_DotId_ReportsWarning()
    {
        var source = "param nicName string\noutput id string = nicName.id";
        var checker = Check(source);
        Assert.Contains(checker.Warnings, d => d.Code == "BCP007");
    }

    [Fact]
    public void BCP007_ResourceUsedAsResource_DotId_NoWarning()
    {
        // 'storageAccount' is declared as a resource — .id is valid
        var source = "resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = { }\n" +
                     "output id string = storageAccount.id";
        var checker = Check(source);
        Assert.DoesNotContain(checker.Warnings, d => d.Code == "BCP007");
    }

    [Fact]
    public void BCP007_VarDotOutputs_ReportsWarning()
    {
        var source = "var myModule = 'something'\noutput x string = myModule.outputs";
        var checker = Check(source);
        Assert.Contains(checker.Warnings, d => d.Code == "BCP007");
    }

    [Fact]
    public void BCP007_NonResourceProperty_NoWarning()
    {
        // Accessing a non-resource-specific member (e.g. .name) on a var is fine
        var source = "var sku = 'Standard_LRS'\nvar x = sku.name";
        var checker = Check(source);
        Assert.DoesNotContain(checker.Warnings, d => d.Code == "BCP007");
    }

    // ── LINE NUMBER LOCATION ─────────────────────────────────

    [Fact]
    public void Location_BCP001_PointsToParamLine()
    {
        var checker = Check("param count int = 'hello'");
        Assert.Contains("[Line 1,", checker.Errors[0].Location);
    }

    [Fact]
    public void Location_BCP002_PointsToIdentifierLine()
    {
        var source = "var storageSku = 'x'\nvar y = missingName";
        var checker = Check(source);
        var diag = checker.Errors.First(d => d.Code == "BCP002");
        Assert.Contains("[Line 2,", diag.Location);
    }

    [Fact]
    public void Location_BCP004_PointsToResourceLine()
    {
        var source = "param x string = 'a'\nresource sa 'Microsoft.Storage/storageAccounts' = { }";
        var checker = Check(source);
        var diag = checker.Errors.First(d => d.Code == "BCP004");
        Assert.Contains("[Line 2,", diag.Location);
    }

    // ── BCP006 OUTPUT TYPE MISMATCH ──────────────────────────

    [Fact]
    public void BCP006_OutputStringDeclared_IntValue_ReportsWarning()
    {
        var checker = Check("output count string = 42");
        Assert.Contains(checker.Warnings, d => d.Code == "BCP006");
    }

    [Fact]
    public void BCP006_OutputTypeMatch_NoWarning()
    {
        var checker = Check("output name string = 'hello'");
        Assert.DoesNotContain(checker.Warnings, d => d.Code == "BCP006");
    }
}
