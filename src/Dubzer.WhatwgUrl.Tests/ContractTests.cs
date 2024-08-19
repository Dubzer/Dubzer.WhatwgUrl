using System.Reflection;

namespace Dubzer.WhatwgUrl.Tests;

public class ContractTests
{
    [Fact]
    public Task VerifyAssemblyContract()
    {
        var publicTypes = typeof(DomUrl).Assembly
            .GetTypes()
            .Where(x => x.IsPublic);

        var dictionary = new Dictionary<Type, string[]>();
        foreach (var type in publicTypes)
        {
            dictionary[type] = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(x => x.ToString()!)
                .Order()
                .ToArray();
        }

        return Verify(dictionary);
    }
