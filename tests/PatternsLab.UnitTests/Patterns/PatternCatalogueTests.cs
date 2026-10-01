using PatternsLab.Patterns;
using Shouldly;

namespace PatternsLab.UnitTests.Patterns;

public class PatternCatalogueTests
{
    private static PatternCatalogue Build()
    {
        var demos = PatternsServiceCollectionExtensions.DiscoverDemoTypes()
            .Select(t => (IPatternDemo)Activator.CreateInstance(t)!)
            .ToList();

        return new PatternCatalogue(demos);
    }

    [Fact]
    public void All_23_gang_of_four_patterns_are_present()
    {
        Build().Count.ShouldBe(23);
    }

    [Theory]
    [InlineData(PatternCategory.Creational, 5)]
    [InlineData(PatternCategory.Structural, 7)]
    [InlineData(PatternCategory.Behavioural, 11)]
    public void Each_category_has_the_expected_number_of_patterns(PatternCategory category, int expected)
    {
        Build().ByCategory(category).Count.ShouldBe(expected);
    }

    [Fact]
    public void Every_gang_of_four_name_is_covered()
    {
        string[] expected =
        [
            "abstract-factory", "builder", "factory-method", "prototype", "singleton",
            "adapter", "bridge", "composite", "decorator", "facade", "flyweight", "proxy",
            "chain-of-responsibility", "command", "interpreter", "iterator", "mediator",
            "memento", "observer", "state", "strategy", "template-method", "visitor"
        ];

        var catalogue = Build();

        foreach (var key in expected)
            catalogue.Find(key).ShouldNotBeNull($"'{key}' is missing from the catalogue.");
    }

    [Fact]
    public void Every_demo_runs_and_produces_a_transcript()
    {
        var catalogue = Build();

        foreach (var pattern in catalogue.All())
        {
            var transcript = catalogue.Run(pattern.Key);

            transcript.ShouldNotBeEmpty($"{pattern.Name} produced no output.");
            transcript.ShouldAllBe(line => line != null);
        }
    }

    [Fact]
    public void Every_demo_declares_its_metadata()
    {
        foreach (var pattern in Build().All())
        {
            pattern.Intent.ShouldNotBeNullOrWhiteSpace();
            pattern.HandbookSection.ShouldNotBeNullOrWhiteSpace();
            pattern.SeenInDotNet.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void An_unknown_key_fails_with_a_message_that_lists_the_known_ones()
    {
        Should.Throw<KeyNotFoundException>(() => Build().Run("singleton-factory-bridge"))
              .Message.ShouldContain("strategy");
    }
}
