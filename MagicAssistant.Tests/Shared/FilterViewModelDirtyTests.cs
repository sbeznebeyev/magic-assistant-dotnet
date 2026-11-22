using MagicAssistant.App.Shared;

namespace MagicAssistant.Tests.Shared;

/// <summary>
/// Commit tracking (IsDirty) and the active-filter badge count on FilterViewModel.
/// </summary>
[TestFixture]
public class FilterViewModelDirtyTests
{
    [Test]
    public void FreshViewModel_IsClean()
    {
        var vm = new FilterViewModel();
        Assert.That(vm.IsDirty, Is.False);
    }

    [Test]
    public void FieldChange_MakesDirty_ApplyClearsIt()
    {
        var vm = new FilterViewModel();

        vm.NameFilter = "lightning";
        Assert.That(vm.IsDirty, Is.True, "uncommitted edit must light the Apply button");

        vm.Apply();
        Assert.That(vm.IsDirty, Is.False, "Apply re-baselines the committed snapshot");
    }

    [Test]
    public void ListChange_MakesDirty()
    {
        var vm = new FilterViewModel();
        vm.Apply();

        vm.Rarity.Add("rare");
        Assert.That(vm.IsDirty, Is.True, "list mutations are compared by content, not reference");
    }

    [Test]
    public void MarkCommitted_ClearsDirty_WithoutFiringChanged()
    {
        var vm = new FilterViewModel();
        var fired = false;
        vm.Changed += () => fired = true;

        vm.SetNodeScope(new[] { "m19" });   // internal auto-fill path
        Assert.That(vm.IsDirty, Is.True);

        vm.MarkCommitted();
        Assert.That(vm.IsDirty, Is.False);
        Assert.That(fired, Is.False, "housekeeping must not trigger a re-query");
    }

    [Test]
    public void SortChange_CountsAsDirty_ButNotAsActiveFilter()
    {
        var vm = new FilterViewModel();

        vm.SortField = "Cmc";
        Assert.That(vm.IsDirty, Is.True);
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(0), "sort is not a filter badge");
    }

    [Test]
    public void ActiveFilterCount_CountsGroupsNotValues()
    {
        var vm = new FilterViewModel
        {
            SearchQuery = "storm",
            Ownership = "owned",
        };
        vm.Rarity.AddRange(new[] { "rare", "mythic" });

        Assert.That(vm.ActiveFilterCount, Is.EqualTo(3),
            "search + rarity group + ownership = 3, regardless of how many rarities");
    }

    [Test]
    public void Clear_LeavesNoActiveFilters()
    {
        var vm = new FilterViewModel { SearchQuery = "storm", Artist = "x" };
        vm.TypeFilter.Add("Creature");

        vm.Clear();

        Assert.That(vm.ActiveFilterCount, Is.EqualTo(0));
    }
}
