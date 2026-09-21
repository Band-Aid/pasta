using System;

public enum KitchenWeapon { Fusilli, Farfalle, Ravioli }
public enum PastaIngredient { Macaroni, Cheese, Ham }

/// <summary>Run-local unlocks. Ingredients are retained, so recipes work in any pickup order.</summary>
public sealed class KitchenInventory
{
    public const int MaxWeaponLevel = 3;
    private readonly int[] weapons = new int[3];
    private readonly int[] ingredients = new int[3];
    public int Revision { get; private set; }
    public int TotalIngredients { get; private set; }
    public int MacAndCheeseParts
    {
        get
        {
            int count = 0;
            for (int i = 0; i < 2; i++) if (ingredients[i] > 0) count++;
            return count;
        }
    }
    public bool HasMacAndCheese => MacAndCheeseParts == 2;
    public bool HasBritishCarbonara => HasMacAndCheese && Count(PastaIngredient.Ham) > 0;
    public int RecipeLevel => HasMacAndCheese ? Math.Min(3, 1 + Math.Max(0, TotalIngredients - 2) / 4) : 0;
    public int Level(KitchenWeapon weapon) => weapons[(int)weapon];
    public int Count(PastaIngredient ingredient) => ingredients[(int)ingredient];

    public bool Upgrade(KitchenWeapon weapon)
    {
        int i = (int)weapon;
        if (weapons[i] >= MaxWeaponLevel) return false;
        weapons[i]++;
        Revision++;
        return true;
    }

    public void Collect(PastaIngredient ingredient)
    {
        ingredients[(int)ingredient]++;
        TotalIngredients++;
        Revision++;
    }

    public void Reset()
    {
        Array.Clear(weapons, 0, weapons.Length);
        Array.Clear(ingredients, 0, ingredients.Length);
        TotalIngredients = 0;
        Revision++;
    }

    // Missing ingredients are more likely, but duplicates remain possible.
    public PastaIngredient RollIngredient(Random random)
    {
        int total = 0;
        for (int i = 0; i < ingredients.Length; i++) total += ingredients[i] == 0 ? 4 : 1;
        int roll = random.Next(total);
        for (int i = 0; i < ingredients.Length; i++)
        {
            roll -= ingredients[i] == 0 ? 4 : 1;
            if (roll < 0) return (PastaIngredient)i;
        }
        return PastaIngredient.Ham;
    }

    public bool TryRollWeapon(Random random, out KitchenWeapon weapon)
    {
        int start = random.Next(weapons.Length);
        for (int offset = 0; offset < weapons.Length; offset++)
        {
            weapon = (KitchenWeapon)((start + offset) % weapons.Length);
            if (Level(weapon) < MaxWeaponLevel) return true;
        }
        weapon = default;
        return false;
    }

    public static string WeaponName(KitchenWeapon weapon) => weapon == KitchenWeapon.Fusilli ? "フジッリ"
        : weapon == KitchenWeapon.Farfalle ? "ファルファッレ" : "ラビオリ";
    public static string IngredientName(PastaIngredient ingredient) => ingredient == PastaIngredient.Macaroni ? "マカロニ"
        : ingredient == PastaIngredient.Cheese ? "チーズ" : "ハム";
}
