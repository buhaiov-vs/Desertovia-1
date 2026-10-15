using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Rules;
using Desertod.Domain.Technologies;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class VigorDrinkTests
{
    [Fact]
    public void Drink_FromStorage_GivesEnergy()
    {
        var village = NewVillage();
        village.Store(ResourceKind.VigorDrink, 1);
        var citizen = NewCitizen(village);
        citizen.StoredEnergy = 10;

        VigorDrinks.Drink(citizen, village, Now);

        Assert.Equal(10 + GameRules.VigorDrinkEnergy, citizen.CurrentEnergy(Now, village), 6);
        Assert.Equal(0, village.Stock(ResourceKind.VigorDrink));
        Assert.Equal(Now + GameRules.VigorDrinkCooldown, VigorDrinks.ReadyAt(citizen));
    }

    [Fact]
    public void Drink_TakesFromHandsFirst()
    {
        var village = NewVillage();
        village.Store(ResourceKind.VigorDrink, 1);
        var citizen = NewCitizen(village);
        citizen.StoredEnergy = 10;
        citizen.Carry(ResourceKind.VigorDrink, 1);

        VigorDrinks.Drink(citizen, village, Now);

        Assert.Equal(0, citizen.Carried(ResourceKind.VigorDrink));
        Assert.Equal(1, village.Stock(ResourceKind.VigorDrink));
    }

    [Fact]
    public void Drink_NeverGoesAboveTheMaximum()
    {
        var village = NewVillage();
        village.Store(ResourceKind.VigorDrink, 1);
        var citizen = NewCitizen(village);
        citizen.StoredEnergy = citizen.MaximumEnergy - 5;

        VigorDrinks.Drink(citizen, village, Now);

        Assert.Equal(citizen.MaximumEnergy, citizen.CurrentEnergy(Now, village), 6);
    }

    [Fact]
    public void Drink_WaitsForTheCooldown()
    {
        var village = NewVillage();
        village.Store(ResourceKind.VigorDrink, 2);
        var citizen = NewCitizen(village);
        citizen.StoredEnergy = 0;

        VigorDrinks.Drink(citizen, village, Now);
        var error = Assert.Throws<GameRuleException>(() => VigorDrinks.Drink(citizen, village, Now.AddHours(1)));
        Assert.Equal("drink.cooldown", error.Code);

        var later = Now + GameRules.VigorDrinkCooldown;
        citizen.StoredEnergy = 0;
        citizen.EnergyUpdatedAt = later;
        VigorDrinks.Drink(citizen, village, later);
        Assert.Equal(0, village.Stock(ResourceKind.VigorDrink));
    }

    [Fact]
    public void Drink_NeedsADrink_AndRoomForEnergy()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);

        Assert.Equal("drink.full", Assert.Throws<GameRuleException>(() => VigorDrinks.Drink(citizen, village, Now)).Code);

        citizen.StoredEnergy = 10;
        Assert.Equal("drink.none", Assert.Throws<GameRuleException>(() => VigorDrinks.Drink(citizen, village, Now)).Code);
        Assert.Null(citizen.LastDrinkAt);
    }

    [Fact]
    public void Personal_drink_uses_owned_stock_and_preserves_cooldown_and_common_supply()
    {
        var village = NewVillage();
        village.Store(ResourceKind.VigorDrink, 2);
        var citizen = NewCitizen(village);
        citizen.StoredEnergy = 10;
        PersonalInventory.Add(citizen, ResourceKind.VigorDrink, 2, 60);

        VigorDrinks.Drink(citizen, village, Now);

        Assert.Equal(10 + GameRules.VigorDrinkEnergy, citizen.CurrentEnergy(Now, village), 6);
        Assert.Equal(1, PersonalInventory.Amount(citizen, ResourceKind.VigorDrink));
        Assert.Equal(30, Assert.Single(citizen.PersonalGoods).CostBasis);
        Assert.Equal(2, village.Stock(ResourceKind.VigorDrink));
        Assert.Equal("drink.cooldown", Assert.Throws<GameRuleException>(() => VigorDrinks.Drink(citizen, village, Now)).Code);
        Assert.Equal(1, PersonalInventory.Amount(citizen, ResourceKind.VigorDrink));
    }

    [Fact]
    public void Brewing_NeedsTheTechnology()
    {
        var task = TestWorld.Task("brew-vigor");

        Assert.Equal(TechnologyCatalog.VigorDrinks, task.RequiredTechnology);
        Assert.Contains(TechnologyCatalog.Brewing, TechnologyCatalog.Get(TechnologyCatalog.VigorDrinks).Requires);
    }
}
