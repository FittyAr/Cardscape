using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Cardscape.ArchitectureTests;

/// <summary>
/// Enums are camelCase strings on the wire (docs/api/00-conventions.md).
/// The API serialises them with <c>JsonStringEnumConverter</c>, which
/// only applies to enum-typed members: an Application DTO that casts an
/// enum to <c>int</c> leaks the numeric CLR value instead, and the Web
/// client (which reads enums as names, integers rejected) fails to
/// deserialise the response. These rules pair each Application DTO with
/// the Web DTO of the same name and pin the two sides together.
/// </summary>
public sealed class WireEnumContractTests
{
    [Fact]
    public void ApplicationDtos_ExposeEnumTypedMembers_WhereWebDtosExpectEnums()
    {
        string[] violations = MatchingProperties()
            .Where(pair => UnwrapNullable(pair.Web.PropertyType).IsEnum
                && !UnwrapNullable(pair.Application.PropertyType).IsEnum)
            .Select(pair => $"{pair.Application.DeclaringType!.FullName}.{pair.Application.Name} is "
                + $"{pair.Application.PropertyType.Name} but Web expects enum "
                + $"{UnwrapNullable(pair.Web.PropertyType).Name}")
            .ToArray();

        violations.Should().BeEmpty(
            "an int-typed field serialises as a number while the Web client only accepts enum names");
    }

    [Fact]
    public void WebEnums_Name_Every_Member_The_Application_Enum_Can_Send()
    {
        string[] violations = MatchingProperties()
            .Select(pair => (
                pair.Application,
                ApplicationEnum: UnwrapNullable(pair.Application.PropertyType),
                WebEnum: UnwrapNullable(pair.Web.PropertyType)))
            .Where(pair => pair.ApplicationEnum.IsEnum && pair.WebEnum.IsEnum)
            .SelectMany(pair => Enum.GetNames(pair.ApplicationEnum)
                .Except(Enum.GetNames(pair.WebEnum), StringComparer.Ordinal)
                .Select(missing => $"{pair.Application.DeclaringType!.FullName}.{pair.Application.Name}: "
                    + $"{pair.ApplicationEnum.Name}.{missing} has no {pair.WebEnum.Name} counterpart"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "every name the API can put on the wire must deserialise on the Web client");
    }

    private static IEnumerable<(PropertyInfo Application, PropertyInfo Web)> MatchingProperties()
    {
        Dictionary<string, Type[]> webDtos = typeof(Cardscape.Web.Shared.BoardDto).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "Cardscape.Web.Shared" && IsDto(type))
            .GroupBy(type => type.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        IEnumerable<Type> applicationDtos = typeof(Cardscape.Application.Cards.CardscapeExtensions).Assembly
            .GetTypes()
            .Where(IsDto);

        foreach (Type applicationDto in applicationDtos)
        {
            if (!webDtos.TryGetValue(applicationDto.Name, out Type[]? webCandidates))
            {
                continue;
            }

            foreach (PropertyInfo applicationProperty in PublicProperties(applicationDto))
            {
                foreach (Type webDto in webCandidates)
                {
                    PropertyInfo? webProperty = webDto.GetProperty(
                        applicationProperty.Name, BindingFlags.Instance | BindingFlags.Public);
                    if (webProperty is not null)
                    {
                        yield return (applicationProperty, webProperty);
                    }
                }
            }
        }
    }

    private static bool IsDto(Type type) =>
        type is { IsPublic: true, IsClass: true } && type.Name.EndsWith("Dto", StringComparison.Ordinal);

    private static PropertyInfo[] PublicProperties(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

    private static Type UnwrapNullable(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}
