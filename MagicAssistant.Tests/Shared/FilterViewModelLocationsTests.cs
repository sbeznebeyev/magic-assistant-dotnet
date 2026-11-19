using MagicAssistant.App.Shared;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Tests.Shared;

/// <summary>
/// The Locations filter state on FilterViewModel: chips as a filter (Clear empties them),
/// InLocationsMode as navigation state (Clear preserves it), and the single-chip invariant.
/// </summary>
[TestFixture]
public class FilterViewModelLocationsTests
{
    [Test]
    public void Clear_EmptiesChips_ButKeepsLocationsMode()
    {
        var vm = new FilterViewModel
        {
            InLocationsMode = true,
            CurrentCollectionId = 7,
            CurrentCollectionNodeType = NodeType.Deck,
        };
        vm.SetLocationChips(new[] { 7, 8 });
        vm.NameFilter = "lightning";

        vm.Clear();

        // Agreed Clear All semantics: chips are a filter — cleared → search across ALL locations.
        Assert.That(vm.LocationNodeIds, Is.Empty);
        // InLocationsMode is navigation state — the KB button owns it, Clear must not eject to KB.
        Assert.That(vm.InLocationsMode, Is.True);
        Assert.That(vm.NameFilter, Is.Empty);
    }

    [Test]
    public void SetLocationChips_Deduplicates()
    {
        var vm = new FilterViewModel { InLocationsMode = true };

        vm.SetLocationChips(new[] { 1, 2, 2, 1, 3 });

        Assert.That(vm.LocationNodeIds, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void SetLocationChips_Null_ResetsToAllLocations()
    {
        var vm = new FilterViewModel { InLocationsMode = true };
        vm.SetLocationChips(new[] { 5 });

        vm.SetLocationChips(null);

        Assert.That(vm.LocationNodeIds, Is.Empty, "empty chips = every location, not 'no locations'");
    }

    [Test]
    public void IsLocationsBrowse_TrueOnlyWithoutSingleChip()
    {
        var vm = new FilterViewModel();

        Assert.That(vm.IsLocationsBrowse, Is.False, "KB mode is not locations browse");

        vm.InLocationsMode = true;
        vm.CurrentCollectionId = 7;
        Assert.That(vm.IsLocationsBrowse, Is.False, "exactly one chip = deck mode with full operations");

        vm.CurrentCollectionId = null;
        vm.SetLocationChips(new[] { 7, 8 });
        Assert.That(vm.IsLocationsBrowse, Is.True, "≥2 chips = KB-style browse over the selected nodes");

        vm.SetLocationChips(null);
        Assert.That(vm.IsLocationsBrowse, Is.True, "0 chips = browse across all locations");
    }
}
